using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.sdk.repo;
using dnproto.sdk.ws;
using dnproto.sdk.fs;

namespace dnproto.cli.commands;

public class BackupAccount : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"actor"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[] { "getprefs", "getrepo", "getblobs", "blobsleepseconds"});
    }

    /// <summary>
    /// Backup an account to local directory (repo, blobs, prefs)
    /// inspired by: https://www.da.vidbuchanan.co.uk/blog/adversarial-pds-migration.html
    /// (this article shows what items to backup for an account, if you need to migrate it)
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments.
        //

        // required
        string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");

        // optional
        bool getPrefs = CommandLineInterface.GetArgumentValueWithDefault(arguments, "getprefs", true);
        bool getRepo = CommandLineInterface.GetArgumentValueWithDefault(arguments, "getrepo", true);
        bool getBlobs = CommandLineInterface.GetArgumentValueWithDefault(arguments, "getblobs", true);
        int blobSleepSeconds = CommandLineInterface.GetArgumentValueWithDefault(arguments, "blobsleepseconds", 1);

        Logger.LogTrace($"actor: {actor}");
        Logger.LogTrace($"dataDir: {dataDir}");
        Logger.LogTrace($"getPrefs: {getPrefs}");
        Logger.LogTrace($"getRepo: {getRepo}");
        Logger.LogTrace($"getBlobs: {getBlobs}");
        Logger.LogTrace($"blobSleepSeconds: {blobSleepSeconds}");

        if (string.IsNullOrEmpty(dataDir) || string.IsNullOrEmpty(actor))
        {
            Logger.LogError("Missing required argument.");
            return;
        }


        //
        // Get local file system and actor info
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
        ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

        if (lfs == null)
        {
            Logger.LogError("Failed to initialize local file system.");
            return;
        }



        //
        // Load session
        //
        SessionFile? session = lfs?.LoadSession(actorInfo);
        if (session == null)
        {
            Logger.LogError($"Failed to load session for actor: {actor}. Please log in to your account.");
            return;
        }

        //
        // Get prefs, to check session.
        //
        Logger.LogInfo("Verifying session by calling getPreferences...");
        JsonNode? prefsTest = BlueskyClient.SendRequest($"https://{session.pds}/xrpc/app.bsky.actor.getPreferences",
            HttpMethod.Get,
            accessJwt: session.accessJwt);
            
        if (prefsTest == null)
        {
            Logger.LogError("Failed to verify session with getPreferences call. Is the session still valid?");
            return;
        }


        //
        // Get backup dir
        //
        string? backupDir = lfs?.GetPath_AccountBackupDir(actorInfo);
        if (string.IsNullOrEmpty(backupDir))
        {
            Logger.LogError("Failed to get backup directory.");
            return;
        }

        if(Directory.Exists(backupDir) == false)
        {
            Logger.LogTrace($"Creating backup directory: {backupDir}");
            Directory.CreateDirectory(backupDir);
        }

        //
        // Create readme.txt
        //
        string readmePath = Path.Combine(backupDir, "README.txt");
        string readmeContents = "Account backup for Bluesky account. Created by dnproto.\n\n";
        readmeContents += $"actor: {actor}\n";
        readmeContents += $"backupDir: {backupDir}\n";
        readmeContents += $"getPrefs: {getPrefs}\n";
        readmeContents += $"getRepo: {getRepo}\n";
        readmeContents += $"getBlobs: {getBlobs}\n";
        Logger.LogInfo($"Creating readme file: {readmePath}");
        File.WriteAllText(readmePath, readmeContents);

        if(!File.Exists(readmePath))
        {
            Logger.LogError("Failed to create readme file.");
            return;
        }


        //
        // Resolve actor
        //

        string? did = actorInfo?.Did;
        string? pds = actorInfo?.Pds;

        if(string.IsNullOrEmpty(did) || string.IsNullOrEmpty(pds))
        {
            Logger.LogError("Failed to resolve actor to did and pds.");
            return;
        }

        Logger.LogInfo($"Resolved handle to did: {did}");
        Logger.LogInfo($"Resolved handle to pds: {pds}");

        //
        // Get prefs
        //
        if(getPrefs)
        {
            //
            // Call WS
            //
            JsonNode? response = BlueskyClient.SendRequest($"https://{session.pds}/xrpc/app.bsky.actor.getPreferences",
                HttpMethod.Get, 
                accessJwt: session.accessJwt);

            if (response == null)
            {
                Logger.LogError("Failed to get preferences.");
                return;
            }

            //
            // Write to disk.
            //
            string prefsFile = Path.Combine(backupDir, "prefs.json");
            Logger.LogInfo("");
            Logger.LogInfo($"----- PREFS -----");
            Logger.LogInfo($"Creating prefs file: {prefsFile}");
            JsonData.WriteJsonToFile(response, prefsFile);

        }


        //
        // Get repo
        //
        if(getRepo)
        {
            string repoFile = Path.Combine(backupDir, "repo.car");
            Logger.LogInfo("");
            Logger.LogInfo($"----- REPO -----");
            Logger.LogInfo($"Getting repo file: {repoFile}");
            BlueskyClient.GetRepo(pds, did, repoFile);
        }



        //
        // Get blobs
        //
        if (getBlobs)
        {
            //
            // List blobs (this just gives you the blob IDs)
            //
            List<string> blobs = BlueskyClient.ListBlobs(session.pds, session.did);
            string blobFile = Path.Combine(backupDir, "blobs.txt");
            Logger.LogInfo("");
            Logger.LogInfo($"----- BLOBS -----");
            Logger.LogInfo($"Found {blobs.Count} blobs.");
            Logger.LogInfo($"Creating blob list file: {blobFile}");
            File.WriteAllLines(blobFile, blobs);


            //
            // Create blobs directory
            //
            string blobsDirectory = Path.Combine(backupDir, "blobs");
            if (!Directory.Exists(blobsDirectory))
            {
                Logger.LogInfo($"Creating blobs directory: {blobsDirectory}");
                Directory.CreateDirectory(blobsDirectory);
            }

            if (!Directory.Exists(blobsDirectory))
            {
                Logger.LogError("Failed to create blobs directory.");
                return;
            }


            //
            // Get all blobs
            //
            int blobCountDownloaded = 0;
            int blobCountSkipped = 0;

            foreach (string blob in blobs)
            {
                string blobPath = Path.Combine(blobsDirectory, blob);

                if (File.Exists(blobPath) == false)
                {
                    Logger.LogInfo($"Downloading blob: {blobPath}");
                    BlueskyClient.GetBlob(session.pds, session.did, blob, blobPath);
                    Thread.Sleep(blobSleepSeconds * 1000);
                    blobCountDownloaded++;
                }
                else
                {
                    Logger.LogTrace($"Blob file already exists, skipping: {blobPath}");
                    blobCountSkipped++;
                }
            }


            //
            // See how many blobs are already in that directory (the user might be deleting posts).
            //
            // list files in the dir
            int blobFileCount = 0;
            try
            {
                blobFileCount = Directory.GetFiles(blobsDirectory).Length;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to list files in blobs directory: {ex.Message}");
            }

            Logger.LogInfo($"Downloaded {blobCountDownloaded} blobs, skipped {blobCountSkipped} blobs. There are {blobFileCount} blob files on disk.");


            //
            // Delete local blob files that are no longer used in the account.
            //
            string[] filesOnDisk = Directory.GetFiles(blobsDirectory);
            HashSet<string> blobsSet = new HashSet<string>(blobs);

            int deletedCount = 0;
            foreach (string filePath in filesOnDisk)
            {
                string fileName = Path.GetFileName(filePath);
                if (!blobsSet.Contains(fileName))
                {
                    try
                    {
                        Logger.LogInfo($"Deleting old blob file: {filePath}");
                        File.Delete(filePath);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete file {filePath}: {ex.Message}");
                    }
                }
            }

            if (deletedCount > 0)
            {
                Logger.LogInfo($"Deleted {deletedCount} old blob files from disk.");
            }
            else
            {
                Logger.LogInfo("There are no blob files to delete.");
            }

            Logger.LogInfo("");

        }
    }
}