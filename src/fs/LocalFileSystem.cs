using System.Text;
using dnproto.auth;
using dnproto.log;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.fs;

/// <summary>
/// Provides access to the local file system for storing repos and backups.
/// </summary>
public class LocalFileSystem
{
    private string _dataDir;

    private IDnProtoLogger _logger;

    private LocalFileSystem(string dataDir, IDnProtoLogger logger, int cacheExpiryMinutes_Actors, int cacheExpiryMinutes_Sessions)
    {
        _dataDir = dataDir;
        _logger = logger;
        this.cacheExpiryMinutes_Actors = cacheExpiryMinutes_Actors;
        this.cacheExpiryMinutes_Sessions = cacheExpiryMinutes_Sessions;
    }


    private readonly object _lock = new object();

    private Dictionary<string, object> _actorLocks = new Dictionary<string, object>();

    private int cacheExpiryMinutes_Sessions = 60*24;

    private int cacheExpiryMinutes_Actors = 60;

    public string GetDataDir()
    {
        return _dataDir;
    }

    #region INIT

    /// <summary>
    /// Ensure that the root dir exists, and creates subdirs if needed.
    /// </summary>
    /// <param name="dataDir"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static LocalFileSystem Initialize(string? dataDir, IDnProtoLogger logger, int cacheExpiryMinutes_Actors = 60, int cacheExpiryMinutes_Sessions = 60*24)
    {
        if (string.IsNullOrEmpty(dataDir) || Directory.Exists(dataDir) == false)
        {
            throw new Exception($"dataDir is null or does not exist: {dataDir}");
        }

        foreach (string subDir in new string[] { "actors", "backups", "repos", "preferences", "sessions", "pds", "scratch", "logs" })
        {
            string fullSubDir = Path.Combine(dataDir, subDir);
            if (Directory.Exists(fullSubDir) == false)
            {
                logger.LogTrace($"Creating subDir: {fullSubDir}");
                Directory.CreateDirectory(fullSubDir);
            }
        }

        foreach (string subDir in new string[] { "blobs"})
        {
            string fullSubDir = Path.Combine(dataDir, "pds", subDir);
            if (Directory.Exists(fullSubDir) == false)
            {
                logger.LogTrace($"Creating subDir: {fullSubDir}");
                Directory.CreateDirectory(fullSubDir);
            }
        }

        var lfs = new LocalFileSystem(dataDir, logger, cacheExpiryMinutes_Actors, cacheExpiryMinutes_Sessions);
        return lfs;
    }

    #endregion


    #region ACTOR

    /// <summary>
    /// Resolve actor info for the given actor (handle or did).
    /// Tries to use cache (file system). If file is not found or expired, 
    /// resolves via BlueskyClient.
    /// </summary>
    /// <param name="actor"></param>
    /// <returns></returns>
    public ActorInfo? ResolveActorInfo(string? actor)
    {
        //
        // No input? exit
        //
        if (string.IsNullOrEmpty(actor))
        {
            _logger.LogError("[ACTOR LFS] actor is null or empty");
            return null;
        }

        //
        // Let's lock per actor. Don't want other callers to be sitting behind a global lock.
        //
        lock (_lock)
        {
            if (!_actorLocks.ContainsKey(actor))
            {
                _actorLocks[actor] = new object();
            }
        }


        lock (_actorLocks[actor])
        {
            StringBuilder logLine = new StringBuilder($"[ACTOR LFS] {actor}");

            try
            {
                //
                // If the file exists, use that.
                //
                string actorFile = Path.Combine(_dataDir, "actors", GetSafeString(actor) + ".json");
                logLine.Append($" fileExists={File.Exists(actorFile)}");
                if (File.Exists(actorFile))
                {
                    // if the file is expired, don't use it
                    FileInfo fileInfo = new FileInfo(actorFile);
                    float fileAgeMinutes = (float)(DateTime.UtcNow - fileInfo.LastWriteTimeUtc).TotalMinutes;
                    logLine.Append($" fileAgeMinutes={fileAgeMinutes:F1}");

                    if (fileInfo.LastWriteTimeUtc < DateTime.UtcNow.AddMinutes(0 - cacheExpiryMinutes_Actors))
                    {
                        logLine.Append($" fileOld=true");
                        _logger.LogTrace($"Actor info file is older than {cacheExpiryMinutes_Actors} minutes, will re-resolve: {actorFile}");
                    }
                    else
                    {
                        logLine.Append($" fileOld=false");
                        _logger.LogTrace($"Actor info file exists and is recent, loading: {actorFile}");
                        string actorJson = File.ReadAllText(actorFile);
                        _logger.LogTrace($"file text: {actorJson}");
                        var info = ActorInfo.FromJsonString(actorJson);

                        if(info == null || info?.Did == null || string.IsNullOrEmpty(info?.Did))
                        {
                            logLine.Append($" missingDid=true");
                            _logger.LogWarning("[LFS] Actor info loaded from file is missing DID, will re-resolve.");
                        }
                        else
                        {
                            return info;                        
                        }
                    }
                }

                //
                // Otherwise, resolve and save to file.
                //
                logLine.Append($" resolve=true");
                _logger.LogTrace($"Resolving actor info and writing to file: {actorFile}");
                var actorInfo = BlueskyClient.ResolveActorInfo(actor);

                if (actorInfo == null)
                {
                    logLine.Append($" resolveFailed=true");
                    return null;
                }

                logLine.Append($" filePath={actorFile}");
                _logger.LogTrace($"Saving actor info to file: {actorFile}");
                File.WriteAllText(actorFile, actorInfo.ToJsonString() ?? "");

                //
                // return the actor info.
                //
                return actorInfo;
            }
            finally
            {
                _logger.LogInfo(logLine.ToString());
            }
        }
    }

    #endregion


    /// <summary>
    /// Get the path to the repo file for the given ActorInfo.
    /// </summary>
    /// <param name="actorInfo"></param>
    /// <returns></returns>
    public string? GetPath_RepoFile(ActorInfo? actorInfo)
    {
        return GetPath_RepoFile(actorInfo?.Did);
    }

    /// <summary>
    /// Get the path to the repo file for the given did.
    /// </summary>
    /// <param name="did"></param>
    /// <returns></returns>
    public string? GetPath_RepoFile(string? did)
    {
        if (did == null || string.IsNullOrEmpty(did))
        {
            _logger.LogError("did is null or empty.");
            return null;
        }

        string repoDir = Path.Combine(_dataDir, "repos");
        string safeDid = GetSafeString(did);
        string repoFile = Path.Combine(repoDir, $"{safeDid}.car");
        return repoFile;
    }

    /// <summary>
    /// Get path for account backups.
    /// </summary>
    /// <param name="actorInfo"></param>
    /// <returns></returns>
    public string? GetPath_AccountBackupDir(ActorInfo? actorInfo)
    {
        if (actorInfo == null || string.IsNullOrEmpty(actorInfo.Did))
        {
            _logger.LogError("actorInfo is null or empty.");
            return null;
        }

        string backupDir = Path.Combine(_dataDir, "backups");
        string safeDid = GetSafeString(actorInfo.Did);
        string accountBackupDir = Path.Combine(backupDir, safeDid);
        return accountBackupDir;
    }

    /// <summary>
    /// </summary>
    /// <param name="actorInfo"></param>
    /// <returns></returns>
    public string? GetPath_Preferences(ActorInfo? actorInfo)
    {
        if (actorInfo == null || string.IsNullOrEmpty(actorInfo.Did))
        {
            _logger.LogError("actorInfo is null or empty.");
            return null;
        }

        string prefsDir = Path.Combine(_dataDir, "preferences");
        string safeDid = GetSafeString(actorInfo.Did);
        string prefsFile = Path.Combine(prefsDir, $"{safeDid}.json");
        return prefsFile;
    }

    public string? GetPath_ScratchDir()
    {
        string scratchDir = Path.Combine(_dataDir, "scratch");
        return scratchDir;
    }

    /// <summary>
    /// Get the path to the session file for the given handle.
    /// </summary>
    /// <param name="actorInfo"></param>
    /// <returns></returns>
    public string? GetPath_SessionFile(ActorInfo? actorInfo)
    {
        if (actorInfo == null || string.IsNullOrEmpty(actorInfo.Did))
        {
            _logger.LogError("actorInfo is null or empty.");
            return null;
        }

        string sessionDir = Path.Combine(_dataDir, "sessions");
        string safeDid = GetSafeString(actorInfo.Did);
        string sessionFile = Path.Combine(sessionDir, $"{safeDid}.json");
        return sessionFile;
    }

    /// <summary>
    /// Loads a session file from disk. This has the accessJwt for connecting to the PDS.
    /// </summary>
    /// <param name="actorInfo"></param>
    /// <returns></returns>
    public SessionFile? LoadSession(ActorInfo? actorInfo)
    {
        if (actorInfo == null || string.IsNullOrEmpty(actorInfo.Did))
        {
            _logger.LogTrace("actorInfo is null or empty.");
            return null;
        }

        // no file? return
        string? sessionFile = GetPath_SessionFile(actorInfo);
        if (string.IsNullOrEmpty(sessionFile) || File.Exists(sessionFile) == false)
        {
            _logger.LogWarning($"Session file is null or empty: {sessionFile}");
            return null;
        }

        // if session file is old, don't use it
        FileInfo fileInfo = new FileInfo(sessionFile);
        if (fileInfo.LastWriteTimeUtc < DateTime.UtcNow.AddMinutes(0 - cacheExpiryMinutes_Sessions))
        {
            _logger.LogWarning($"Session file is older than {cacheExpiryMinutes_Sessions} minutes, will not use: {sessionFile}");
            return null;
        }

        // can't read json? return
        _logger.LogInfo("Reading session file: " + sessionFile);
        var session = JsonData.ReadJsonFromFile(sessionFile);
        if (session == null)
        {
            _logger.LogWarning($"Failed to read session file: {sessionFile}");
            return null;
        }

        string? accessJwt = JsonData.SelectString(session, "accessJwt");
        string? refreshJwt = JsonData.SelectString(session, "refreshJwt");
        string? pds = JsonData.SelectString(session, "pds");
        string? did = JsonData.SelectString(session, "did");

        // incorrect values? return
        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(accessJwt) || string.IsNullOrEmpty(did) || string.IsNullOrEmpty(refreshJwt))
        {
            _logger.LogWarning("Session file is missing required fields.");
            return null;
        }

        // Print expiration date for accessJwt and refreshJwt
        _logger.LogTrace($"Access JWT expires at: {JwtSecret.GetExpirationDateForAccessJwt(accessJwt).ToLocalTime()} (local time)");
        _logger.LogTrace($"Refresh JWT expires at: {JwtSecret.GetExpirationDateForAccessJwt(refreshJwt).ToLocalTime()} (local time)");

        // if we've gotten this far, return the session file.
        return new SessionFile()
        {
            ActorInfo = actorInfo,
            accessJwt = accessJwt,
            refreshJwt = refreshJwt,
            pds = pds,
            did = did,
            filePath = sessionFile
        };
    }

    public string GetPath_PdsDb()
    {
        string dbFilePath = Path.Combine(_dataDir, "pds", "pds.db");
        return dbFilePath;
    }
    

    /// <summary>
    /// Make a string safe for use as a file or directory name.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public string GetSafeString(string input)
    {
        return input.Replace(":", "_").Replace("/", "_").Replace(".", "_").Replace("@", "_");
    }

    
    /// <summary>
    /// Infers the MIME type from a file path based on the file extension.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>The inferred MIME type.</returns>
    public string InferMimeType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            // Images
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".tiff" or ".tif" => "image/tiff",

            // Video
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".mkv" => "video/x-matroska",
            ".flv" => "video/x-flv",
            ".wmv" => "video/x-ms-wmv",
            ".m4v" => "video/x-m4v",

            // Audio
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            ".flac" => "audio/flac",
            ".aac" => "audio/aac",
            ".wma" => "audio/x-ms-wma",

            // Documents
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".rtf" => "application/rtf",

            // Archives
            ".zip" => "application/zip",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            ".7z" => "application/x-7z-compressed",
            ".rar" => "application/vnd.rar",

            // Code/Web
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".xml" => "application/xml",

            // Default
            _ => "application/octet-stream"
        };
    }
}
