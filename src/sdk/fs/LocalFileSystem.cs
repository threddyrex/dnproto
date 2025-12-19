using dnproto.sdk.log;
using dnproto.sdk.repo;
using dnproto.sdk.ws;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace dnproto.sdk.fs;

/// <summary>
/// Provides access to the local file system for storing repos and backups.
/// </summary>
public class LocalFileSystem(string dataDir, ILogger logger)
{
    public string DataDir = dataDir;

    public ILogger Logger = logger;

    /// <summary>
    /// Ensure that the root dir exists, and creates subdirs if needed.
    /// </summary>
    /// <param name="dataDir"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static LocalFileSystem? Initialize(string? dataDir, ILogger logger)
    {
        if (string.IsNullOrEmpty(dataDir) || Directory.Exists(dataDir) == false)
        {
            logger.LogError($"dataDir does not exist: {dataDir}");
            return null;
        }

        foreach (string subDir in new string[] { "actors", "backups", "repos", "preferences", "sessions", "pds", "scratch" })
        {
            string fullSubDir = Path.Combine(dataDir, subDir);
            if (Directory.Exists(fullSubDir) == false)
            {
                logger.LogTrace($"Creating subDir: {fullSubDir}");
                Directory.CreateDirectory(fullSubDir);
            }
        }

        foreach (string pdsSubDir in new string[] { "accounts"})
        {
            string fullSubDir = Path.Combine(dataDir, "pds", pdsSubDir);
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
            Logger.LogError("lfs.ResolveActorInfo: actor is null or empty.");
            return null;
        }

        string actorFile = Path.Combine(DataDir, "actors", GetSafeString(actor) + ".json");

        //
        // If the file exists, use that.
        //
        if (File.Exists(actorFile))
        {
            // if the file is older than an hour, don't use it
            FileInfo fileInfo = new FileInfo(actorFile);
            if (fileInfo.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-1))
            {
                Logger.LogInfo($"Actor info file is older than 1 hour, will re-resolve: {actorFile}");
            }
            else
            {
                Logger.LogInfo($"Actor info file exists and is recent, loading: {actorFile}");
                string actorJson = File.ReadAllText(actorFile);
                Logger.LogTrace($"file text: {actorJson}");
                var info = ActorInfo.FromJsonString(actorJson);
                return info;
            }
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

        Logger.LogInfo($"Saving actor info to file: {actorFile}");
        File.WriteAllText(actorFile, actorInfo.ToJsonString() ?? "");

        //
        // return the actor info.
        //
        return actorInfo;
    }


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
            Logger.LogError("did is null or empty.");
            return null;
        }

        string repoDir = Path.Combine(DataDir, "repos");
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

    public string? GetPath_ScratchDir()
    {
        string scratchDir = Path.Combine(DataDir, "scratch");
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

        // no file? return
        string? sessionFile = GetPath_SessionFile(actorInfo);
        if (string.IsNullOrEmpty(sessionFile) || File.Exists(sessionFile) == false)
        {
            Logger.LogWarning($"Session file is null or empty: {sessionFile}");
            return null;
        }

        // if session file is older than an hour, don't use it
        FileInfo fileInfo = new FileInfo(sessionFile);
        if (fileInfo.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-1))
        {
            Logger.LogWarning($"Session file is older than 1 hour, will not use: {sessionFile}");
            return null;
        }

        // can't read json? return
        Logger.LogInfo("Reading session file: " + sessionFile);
        var session = JsonData.ReadJsonFromFile(sessionFile);
        if (session == null)
        {
            Logger.LogWarning($"Failed to read session file: {sessionFile}");
            return null;
        }

        string? accessJwt = JsonData.SelectString(session, "accessJwt");
        string? refreshJwt = JsonData.SelectString(session, "refreshJwt");
        string? pds = JsonData.SelectString(session, "pds");
        string? did = JsonData.SelectString(session, "did");

        // incorrect values? return
        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(accessJwt) || string.IsNullOrEmpty(did) || string.IsNullOrEmpty(refreshJwt))
        {
            Logger.LogWarning("Session file is missing required fields.");
            return null;
        }

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

    public string? GetPath_PdsConfig()
    {
        string configFilePath = Path.Combine(DataDir, "pds", "pds-config.json");
        return configFilePath;
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
