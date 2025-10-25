using dnproto.log;
using dnproto.repo;
using dnproto.ws;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Provides access to the local file system for storing repos and backups.
/// </summary>
public class LocalFileSystem(string dataDir, BaseLogger logger)
{
    public string DataDir = dataDir;

    public BaseLogger Logger = logger;

    /// <summary>
    /// Ensure that the root dir exists, and creates subdirs if needed.
    /// </summary>
    /// <param name="dataDir"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static LocalFileSystem? Initialize(string? dataDir, BaseLogger logger)
    {
        if (string.IsNullOrEmpty(dataDir) || Directory.Exists(dataDir) == false)
        {
            logger.LogError($"dataDir does not exist: {dataDir}");
            return null;
        }

        foreach (string subDir in new string[] { "backups", "repos", "preferences", "sessions" })
        {
            string fullSubDir = Path.Combine(dataDir, subDir);
            if (Directory.Exists(fullSubDir) == false)
            {
                logger.LogTrace($"Creating subDir: {fullSubDir}");
                Directory.CreateDirectory(fullSubDir);
            }
        }

        return new LocalFileSystem(dataDir, logger);
    }


    /// <summary>
    /// Get the path to the repo file for the given handle.
    /// </summary>
    /// <param name="handle"></param>
    /// <returns></returns>
    public string? GetPath_RepoFile(HandleInfo? handleInfo)
    {
        if (handleInfo == null || string.IsNullOrEmpty(handleInfo.Did))
        {
            Logger.LogError("handleInfo is null or empty.");
            return null;
        }

        string repoDir = Path.Combine(DataDir, "repos");
        string safeDid = GetSafeString(handleInfo.Did);
        string repoFile = Path.Combine(repoDir, $"{safeDid}.car");
        return repoFile;
    }

    /// <summary>
    /// Get path for account backups.
    /// </summary>
    /// <param name="handle"></param>
    /// <returns></returns>
    public string? GetPath_AccountBackupDir(HandleInfo? handleInfo)
    {
        if (handleInfo == null || string.IsNullOrEmpty(handleInfo.Did))
        {
            Logger.LogError("handleInfo is null or empty.");
            return null;
        }

        string backupDir = Path.Combine(DataDir, "backups");
        string safeDid = GetSafeString(handleInfo.Did);
        string accountBackupDir = Path.Combine(backupDir, safeDid);
        return accountBackupDir;
    }

    /// <summary>
    /// </summary>
    /// <param name="handle"></param>
    /// <returns></returns>
    public string? GetPath_Preferences(HandleInfo? handleInfo)
    {
        if (handleInfo == null || string.IsNullOrEmpty(handleInfo.Did))
        {
            Logger.LogError("handleInfo is null or empty.");
            return null;
        }

        string prefsDir = Path.Combine(DataDir, "preferences");
        string safeDid = GetSafeString(handleInfo.Did);
        string prefsFile = Path.Combine(prefsDir, $"{safeDid}.json");
        return prefsFile;
    }

    /// <summary>
    /// Get the path to the session file for the given handle.
    /// </summary>
    /// <param name="handle"></param>
    /// <returns></returns>
    public string? GetPath_SessionFile(HandleInfo? handleInfo)
    {
        if (handleInfo == null || string.IsNullOrEmpty(handleInfo.Did))
        {
            Logger.LogError("handleInfo is null or empty.");
            return null;
        }

        string sessionDir = Path.Combine(DataDir, "sessions");
        string safeDid = GetSafeString(handleInfo.Did);
        string sessionFile = Path.Combine(sessionDir, $"{safeDid}.json");
        return sessionFile;
    }

    /// <summary>
    /// Loads a session file from disk. This has the accessJwt for connecting to the PDS.
    /// </summary>
    /// <param name="handle"></param>
    /// <returns></returns>
    public SessionFile? LoadSession(HandleInfo? handleInfo)
    {
        if (handleInfo == null || string.IsNullOrEmpty(handleInfo.Did))
        {
            Logger.LogTrace("handleInfo is null or empty.");
            return null;
        }

        string? sessionFile = GetPath_SessionFile(handleInfo);
        if (string.IsNullOrEmpty(sessionFile) || File.Exists(sessionFile) == false)
        {
            Logger.LogTrace($"Session file is null or empty: {sessionFile}");
            return null;
        }

        Logger.LogInfo("Reading session file: " + sessionFile);
        var session = JsonData.ReadJsonFromFile(sessionFile);
        if (session == null)
        {
            Logger.LogTrace($"Failed to read session file: {sessionFile}");
            return null;
        }

        string? accessJwt = JsonData.SelectString(session, "accessJwt");
        string? refreshJwt = JsonData.SelectString(session, "refreshJwt");
        string? pds = JsonData.SelectString(session, "pds");
        string? did = JsonData.SelectString(session, "did");

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(accessJwt) || string.IsNullOrEmpty(did) || string.IsNullOrEmpty(refreshJwt))
        {
            Logger.LogTrace("Session file is missing required fields.");
            return null;
        }

        return new SessionFile()
        {
            HandleInfo = handleInfo,
            accessJwt = accessJwt,
            refreshJwt = refreshJwt,
            pds = pds,
            did = did,
            filePath = sessionFile
        };
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
}
