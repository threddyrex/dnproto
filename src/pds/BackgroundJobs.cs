

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.Http;
using dnproto.fs;
using dnproto.log;
using dnproto.ws;

namespace dnproto.pds;


public class BackgroundJobs
{
    
    private LocalFileSystem _lfs;

    private Logger _logger;

    private PdsDb _db;

    private System.Threading.Timer? _timerLogLevel;
    private System.Threading.Timer? _timerCleanupOldLogs;
    private System.Threading.Timer? _timerDeleteOldFirehoseEvents;
    private System.Threading.Timer? _timerDeleteOldOauthRequests;
    private System.Threading.Timer? _timerRequestCrawl;

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
            foreach(string logFile in Directory.GetFiles(Path.Combine(_lfs.GetDataDir(), "logs")))
            {
                if(File.GetLastWriteTime(logFile) < DateTime.Now.AddDays(-3)
                    && logFile.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                {
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
            bool requestCrawlEnabled = _db.IsRequestCrawlEnabled();

            if(requestCrawlEnabled)
            {
                string pdsHostname = _db.GetConfig().PdsHostname;

                foreach(string crawler in _db.GetPdsCrawlers())
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
}