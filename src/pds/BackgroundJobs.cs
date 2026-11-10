

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.Http;
using System.Xml.Linq;
using dnproto.fs;
using dnproto.log;
using dnproto.ws;

namespace dnproto.pds;


public class BackgroundJobs
{
    
    private LocalFileSystem _lfs;

    private Logger _logger;

    private PdsDb _db;

    private static readonly HttpClient _httpClient = new();

    private System.Threading.Timer? _timerLogLevel;
    private System.Threading.Timer? _timerCleanupOldLogs;
    private System.Threading.Timer? _timerDeleteOldFirehoseEvents;
    private System.Threading.Timer? _timerDeleteOldOauthRequests;
    private System.Threading.Timer? _timerRequestCrawl;
    private System.Threading.Timer? _timerDeleteStaleAdminSessions;
    private System.Threading.Timer? _timerBackupBlobs;
    private int _backupBlobsRunning;

    public BackgroundJobs(LocalFileSystem lfs, Logger logger, PdsDb db)
    {
        _lfs = lfs;
        _logger = logger;
        _db = db;
    }

    public void Start()
    {
        // run Job_UpdateLogLevel every 15 seconds
        _timerLogLevel = new System.Threading.Timer(_ => Job_UpdateLogLevel(), null, TimeSpan.Zero, TimeSpan.FromSeconds(15));
        // run Job_CleanupOldLogs every hour
        _timerCleanupOldLogs = new System.Threading.Timer(_ => Job_CleanupOldLogs(), null, TimeSpan.Zero, TimeSpan.FromHours(1));
        // run Job_DeleteOldFirehoseEvents every hour
        _timerDeleteOldFirehoseEvents = new System.Threading.Timer(_ => Job_DeleteOldFirehoseEvents(), null, TimeSpan.Zero, TimeSpan.FromHours(1));
        // run Job_DeleteOldOauthRequests every hour
        _timerDeleteOldOauthRequests = new System.Threading.Timer(_ => Job_DeleteOldOauthRequests(), null, TimeSpan.Zero, TimeSpan.FromHours(1));
        // request crawl every 5 minutes, but start 30 seconds after process startup
        _timerRequestCrawl = new System.Threading.Timer(_ => Job_RequestCrawlIfEnabled(), null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));
        // delete stale admin sessions every hour
        _timerDeleteStaleAdminSessions = new System.Threading.Timer(_ => Job_DeleteStaleAdminSessions(), null, TimeSpan.FromSeconds(30), TimeSpan.FromHours(1));
        // back up PDS blobs to Azure once per day, starting 60 seconds after startup
        _timerBackupBlobs = new System.Threading.Timer(state => { _ = Job_BackupBlobsToAzureAsync(); }, null, TimeSpan.FromSeconds(60), TimeSpan.FromDays(1));
    }


    private void Job_UpdateLogLevel()
    {
        try
        {
            string currentLevel = _logger.GetLogLevel();
            string newLevel = _db.GetLogLevel();

            if (string.Equals(currentLevel, newLevel, StringComparison.OrdinalIgnoreCase) == false)
            {
                _logger.LogInfo($"[BACKGROUND] UpdateLogLevel currentLevel=[{currentLevel}] newLevel=[{newLevel}]");
                _logger.SetLogLevel(newLevel);
            }            
        }
        catch(Exception ex)
        {
            _logger.LogException(ex);
        }
    }

    private void Job_CleanupOldLogs()
    {
        try
        {
            int logRetentionDays = _db.GetConfigPropertyInt("LogRetentionDays");
            foreach(string logFile in Directory.GetFiles(Path.Combine(_lfs.GetDataDir(), "logs")))
            {
                if(File.GetLastWriteTime(logFile) < DateTime.Now.AddDays(0 - logRetentionDays)
                    && logFile.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                {
                    BackupLogToAzure(logFile);
                    _logger.LogInfo($"[BACKGROUND] Deleting old log file: {logFile}");

                    try
                    {
                        File.Delete(logFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[BACKGROUND] Failed to delete log file: {logFile}. Exception: {ex.Message}");
                    }
                }
                else
                {
                    _logger.LogInfo($"[BACKGROUND] Keeping log file: {logFile}");
                }
            }            
        }
        catch(Exception ex)
        {
            _logger.LogException(ex);
        }
    }

    private void BackupLogToAzure(string logFile)
    {
        try
        {
            string? sasUrl = _db.GetConfigPropertyOptional("AzureStorageSASURL");
            if (string.IsNullOrWhiteSpace(sasUrl))
            {
                _logger.LogWarning($"[BACKGROUND] AzureStorageSASURL is not set; skipping backup of log file: {Path.GetFileName(logFile)}");
                return;
            }

            string pdsHostname = _db.GetConfigPropertyOptional("PdsHostname") ?? "unknown";
            string blobName = $"{pdsHostname}-{Path.GetFileName(logFile)}";
            using var request = new HttpRequestMessage(HttpMethod.Put, GetAzureBlobUri(sasUrl, blobName))
            {
                Content = new ByteArrayContent(File.ReadAllBytes(logFile))
            };
            request.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
            request.Headers.TryAddWithoutValidation("x-ms-version", "2021-08-06");

            using var response = _httpClient.Send(request);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInfo($"[BACKGROUND] Backed up log file to Azure storage as blob: {blobName}");
            }
            else
            {
                _logger.LogError($"[BACKGROUND] Azure log backup failed for blob {blobName} with status {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[BACKGROUND] Azure log backup failed for {Path.GetFileName(logFile)}: {ex.GetType().Name}");
        }
    }

    private async Task Job_BackupBlobsToAzureAsync()
    {
        if (Interlocked.Exchange(ref _backupBlobsRunning, 1) != 0)
        {
            return;
        }

        try
        {
            string? sasUrl = _db.GetConfigPropertyOptional("AzureStorageSASURL_Blobs");
            if (string.IsNullOrWhiteSpace(sasUrl))
            {
                _logger.LogInfo("[BACKGROUND] AzureStorageSASURL_Blobs is not set; skipping blob backup");
                return;
            }

            HashSet<string> containerBlobs = await ListAzureContainerBlobsAsync(sasUrl);
            List<Blob> pdsBlobs = _db.GetAllBlobs();
            List<Blob> missingBlobs = pdsBlobs.Where(blob => !containerBlobs.Contains(blob.Cid)).ToList();

            _logger.LogInfo($"[BACKGROUND] Blob backup containerBlobCount={containerBlobs.Count} pdsBlobCount={pdsBlobs.Count} missingBlobCount={missingBlobs.Count}");

            IBlobDb blobDb = BlobDb.Create(_lfs, _logger);
            int uploaded = 0;
            int failed = 0;

            foreach (Blob blob in missingBlobs)
            {
                try
                {
                    byte[] bytes = blobDb.GetBlobBytes(blob.Cid);
                    using var request = new HttpRequestMessage(HttpMethod.Put, GetAzureBlobUri(sasUrl, blob.Cid))
                    {
                        Content = new ByteArrayContent(bytes)
                    };
                    request.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
                    request.Headers.TryAddWithoutValidation("x-ms-version", "2021-08-06");
                    request.Headers.TryAddWithoutValidation("x-ms-blob-content-type", blob.ContentType);

                    using var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        uploaded++;
                        _logger.LogInfo($"[BACKGROUND] Uploaded blob {blob.Cid} to Azure storage");
                    }
                    else
                    {
                        failed++;
                        _logger.LogError($"[BACKGROUND] Azure blob backup failed for {blob.Cid} with status {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError($"[BACKGROUND] Azure blob backup failed for {blob.Cid}: {ex.GetType().Name}");
                }
            }

            _logger.LogInfo($"[BACKGROUND] Blob backup complete uploaded={uploaded} failed={failed}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            _logger.LogError($"[BACKGROUND] Blob backup job failed with HTTP status {ex.StatusCode.Value}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[BACKGROUND] Blob backup job failed: {ex.GetType().Name}");
        }
        finally
        {
            Volatile.Write(ref _backupBlobsRunning, 0);
        }
    }

    private static async Task<HashSet<string>> ListAzureContainerBlobsAsync(string containerSasUrl)
    {
        var blobNames = new HashSet<string>(StringComparer.Ordinal);
        string? marker = null;

        do
        {
            using var response = await _httpClient.GetAsync(GetAzureListBlobsUri(containerSasUrl, marker));
            response.EnsureSuccessStatusCode();
            string responseXml = await response.Content.ReadAsStringAsync();
            XDocument document = XDocument.Parse(responseXml);

            foreach (XElement name in document.Descendants().Where(element => element.Name.LocalName == "Name"))
            {
                blobNames.Add(name.Value);
            }

            marker = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "NextMarker")
                ?.Value;
            if (string.IsNullOrEmpty(marker))
            {
                marker = null;
            }
        }
        while (marker != null);

        return blobNames;
    }

    private static Uri GetAzureBlobUri(string containerSasUrl, string blobName)
    {
        int queryIndex = containerSasUrl.IndexOf('?');
        string containerUrl = queryIndex < 0 ? containerSasUrl : containerSasUrl[..queryIndex];
        string query = queryIndex < 0 ? "" : containerSasUrl[(queryIndex + 1)..];
        string blobUrl = $"{containerUrl.TrimEnd('/')}/{Uri.EscapeDataString(blobName)}";
        if (!string.IsNullOrEmpty(query))
        {
            blobUrl += $"?{query}";
        }

        return new Uri(blobUrl, UriKind.Absolute);
    }

    private static Uri GetAzureListBlobsUri(string containerSasUrl, string? marker)
    {
        int queryIndex = containerSasUrl.IndexOf('?');
        string containerUrl = queryIndex < 0 ? containerSasUrl : containerSasUrl[..queryIndex];
        string sasQuery = queryIndex < 0 ? "" : containerSasUrl[(queryIndex + 1)..];
        string query = "restype=container&comp=list";
        if (!string.IsNullOrEmpty(sasQuery))
        {
            query += $"&{sasQuery}";
        }
        if (!string.IsNullOrEmpty(marker))
        {
            query += $"&marker={Uri.EscapeDataString(marker)}";
        }

        return new Uri($"{containerUrl.TrimEnd('/')}?{query}", UriKind.Absolute);
    }

    private void Job_DeleteOldFirehoseEvents()
    {
        try
        {
            int oldEventCount = _db.GetCountOfOldFirehoseEvents();
            if (oldEventCount > 0)
            {
                _db.DeleteOldFirehoseEvents();
            }
            int oldEventCountAfter = _db.GetCountOfOldFirehoseEvents();

            _logger.LogInfo($"[BACKGROUND] DeleteOldFirehoseEvents beforeCount={oldEventCount} afterCount={oldEventCountAfter}");            
        }
        catch(Exception ex)
        {
            _logger.LogException(ex);
        }

    }

    private void Job_DeleteOldOauthRequests()
    {
        try
        {
            _logger.LogInfo($"[BACKGROUND] DeleteOldOauthRequests");            
            _db.DeleteOldOauthRequests();
        }
        catch(Exception ex)
        {
            _logger.LogException(ex);
        }
    }

    private void Job_RequestCrawlIfEnabled()
    {
        try
        {
            bool requestCrawlEnabled = _db.GetConfigPropertyBool("FeatureEnabled_RequestCrawl");

            if(requestCrawlEnabled)
            {
                string pdsHostname = _db.GetConfigProperty("PdsHostname");

                string[] crawlers = _db.GetConfigProperty("PdsCrawlers").Split(',', StringSplitOptions.RemoveEmptyEntries);


                foreach(string crawler in crawlers)
                {
                    string url = $"https://{crawler}/xrpc/com.atproto.sync.requestCrawl";
                    JsonObject jsonObject = new JsonObject
                    {
                        ["hostname"] = pdsHostname
                    };

                    JsonNode? response = BlueskyClient.SendRequest(url, HttpMethod.Post, content: new StringContent(JsonSerializer.Serialize(jsonObject)));

                    _logger.LogInfo($"[BACKGROUND] RequestCrawl. pdsHostname={pdsHostname} crawler={crawler} response={response?.ToJsonString()}");
                }
            }
        }
        catch(Exception ex)
        {
            _logger.LogException(ex);
        }
    }


    private void Job_DeleteStaleAdminSessions()
    {
        try
        {
            _logger.LogInfo($"[BACKGROUND] DeleteStaleAdminSessions");            
            _db.DeleteStaleAdminSessions(timeoutMinutes: 120);
        }
        catch(Exception ex)
        {
            _logger.LogException(ex);
        }
    }
}