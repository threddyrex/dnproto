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

        foreach (string subDir in new string[] { "backups", "repos" })
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
    public string? GetPath_RepoFile(string? handle)
    {
        if (string.IsNullOrEmpty(handle))
        {
            Logger.LogError("handle is null or empty.");
            return null;
        }

        string repoDir = Path.Combine(DataDir, "repos");
        string safeHandle = GetSafeString(handle);
        string repoFile = Path.Combine(repoDir, $"{safeHandle}.car");
        return repoFile;
    }


    /// <summary>
    /// Get path for account backups.
    /// </summary>
    /// <param name="handle"></param>
    /// <returns></returns>
    public string? GetPath_AccountBackupDir(string? handle)
    {
        if (string.IsNullOrEmpty(handle))
        {
            Logger.LogError("handle is null or empty.");
            return null;
        }

        string backupDir = Path.Combine(DataDir, "backups");
        string safeHandle = GetSafeString(handle);
        string accountBackupDir = Path.Combine(backupDir, safeHandle);
        return accountBackupDir;
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
