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

        foreach (string subDir in new string[] { "actors", "backups", "repos", "preferences", "sessions" })
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
    /// Resolve actor info for the given actor (handle or did).
    /// </summary>
    /// <param name="actor"></param>
    /// <returns></returns>
    public ActorInfo? ResolveActorInfo(string? actor)
    {
        if (string.IsNullOrEmpty(actor))
        {
            Logger.LogError("actor is null or empty.");
            return null;
        }

        string actorFile = Path.Combine(DataDir, "actors", GetSafeString(actor) + ".json");

        //
        // If the file exists, use that.
        //
        if (File.Exists(actorFile))
        {
            Logger.LogInfo($"Loading actor info from file: {actorFile}");
            string actorJson = File.ReadAllText(actorFile);
            Logger.LogTrace($"file text: {actorJson}");
            var info = ActorInfo.FromJsonString(actorJson);
            return info;
        }

        //
        // Otherwise, resolve and save to file.
        //
        Logger.LogInfo($"Resolving actor info and writing to file: {actorFile}");
        var actorInfo = BlueskyClient.ResolveActorInfo(actor);

        if (actorInfo == null)
        {
            Logger.LogError("Failed to resolve actor info.");
            return null;
        }

        Logger.LogTrace($"Saving actor info to file: {actorFile}");
        File.WriteAllText(actorFile, actorInfo.ToJsonString() ?? "");

        //
        // return the actor info.
        //
        return actorInfo;
    }


    /// <summary>
    /// Get the path to the repo file for the given handle.
    /// </summary>
    /// <param name="actorInfo"></param>
    /// <returns></returns>
    public string? GetPath_RepoFile(ActorInfo? actorInfo)
    {
        if (actorInfo == null || string.IsNullOrEmpty(actorInfo.Did))
        {
            Logger.LogError("actorInfo is null or empty.");
            return null;
        }

        string repoDir = Path.Combine(DataDir, "repos");
        string safeDid = GetSafeString(actorInfo.Did);
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
            Logger.LogError("actorInfo is null or empty.");
            return null;
        }

        string backupDir = Path.Combine(DataDir, "backups");
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
            Logger.LogError("actorInfo is null or empty.");
            return null;
        }

        string prefsDir = Path.Combine(DataDir, "preferences");
        string safeDid = GetSafeString(actorInfo.Did);
        string prefsFile = Path.Combine(prefsDir, $"{safeDid}.json");
        return prefsFile;
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
            Logger.LogError("actorInfo is null or empty.");
            return null;
        }

        string sessionDir = Path.Combine(DataDir, "sessions");
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
            Logger.LogTrace("actorInfo is null or empty.");
            return null;
        }

        string? sessionFile = GetPath_SessionFile(actorInfo);
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
            ActorInfo = actorInfo,
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
