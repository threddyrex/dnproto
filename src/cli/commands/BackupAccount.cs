using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class BackupAccount : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"handle", "password", "dataDir"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[] { "getprefs", "getrepo", "getblobs", "blobsleepseconds", "authFactorToken" });
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
        string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");
        string? password = CommandLineInterface.GetArgumentValue(arguments, "password");
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");

        // optional
        string? authFactorToken = CommandLineInterface.GetArgumentValue(arguments, "authFactorToken");
        bool getPrefs = CommandLineInterface.GetArgumentValueWithDefault(arguments, "getprefs", true);
        bool getRepo = CommandLineInterface.GetArgumentValueWithDefault(arguments, "getrepo", true);
        bool getBlobs = CommandLineInterface.GetArgumentValueWithDefault(arguments, "getblobs", true);
        int blobSleepSeconds = CommandLineInterface.GetArgumentValueWithDefault(arguments, "blobsleepseconds", 1);

        Logger.LogInfo($"handle: {handle}");
        Logger.LogInfo($"dataDir: {dataDir}");
        Logger.LogInfo($"getPrefs: {getPrefs}");
        Logger.LogInfo($"getRepo: {getRepo}");
        Logger.LogInfo($"getBlobs: {getBlobs}");
        Logger.LogInfo($"password length: {password?.Length}");
        Logger.LogInfo($"authFactorToken length: {authFactorToken?.Length}");

        if(string.IsNullOrEmpty(dataDir) || string.IsNullOrEmpty(handle) || string.IsNullOrEmpty(password))
        {
            Logger.LogError("Missing required argument.");
            return;
        }

        //
        // Get local file system
        //
        LocalFileSystem? localFileSystem = LocalFileSystem.Initialize(dataDir, Logger);
        if (localFileSystem == null)
        {
            Logger.LogError("Failed to initialize local file system.");
            return;
        }


        //
        // Get backup dir
        //
        string? backupDir = localFileSystem.GetPath_AccountBackupDir(handle);
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
        readmeContents += $"handle: {handle}\n";
        readmeContents += $"backupDir: {backupDir}\n";
        readmeContents += $"getPrefs: {getPrefs}\n";
        readmeContents += $"getRepo: {getRepo}\n";
        readmeContents += $"getBlobs: {getBlobs}\n";
        readmeContents += $"password length: {password?.Length}\n";
        readmeContents += $"authFactorToken length: {authFactorToken?.Length}\n";
        Logger.LogInfo($"Creating readme file: {readmePath}");
        File.WriteAllText(readmePath, readmeContents);

        if(!File.Exists(readmePath))
        {
            Logger.LogError("Failed to create readme file.");
            return;
        }


        //
        // Resolve handle
        //
        Dictionary<string, string> handleInfo = BlueskyClient.ResolveHandleInfo(handle);

        string? did = handleInfo.ContainsKey("did") ? handleInfo["did"] : null;
        string? pds = handleInfo.ContainsKey("pds") ? handleInfo["pds"] : null;

        if(string.IsNullOrEmpty(did) || string.IsNullOrEmpty(pds))
        {
            Logger.LogError("Failed to resolve handle to did and pds.");
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
            // Create session (log in)
            //
            JsonNode? session = BlueskyClient.CreateSession(pds, handle, password, authFactorToken);

            string? accessJwt = JsonData.SelectString(session, "accessJwt");

            if (session == null || string.IsNullOrEmpty(accessJwt))
            {
                Logger.LogError("FAILED TO CREATE SESSION! Either handle/password is incorrect, or you need to provide the authFactor token from email.");
                return;
            }
            else
            {
                Logger.LogInfo($"Successfully created session.");
            }


            //
            // Call WS
            //
            JsonNode? response = BlueskyClient.SendRequest($"https://{pds}/xrpc/app.bsky.actor.getPreferences",
                HttpMethod.Get, 
                accessJwt: accessJwt);


            //
            // Write to disk.
            //
            string prefsFile = Path.Combine(backupDir, "prefs.json");
            Logger.LogInfo($"(PREFS)");
            Logger.LogInfo($"Creating prefs file: {prefsFile}");
            JsonData.WriteJsonToFile(response, prefsFile);

        }


        //
        // Get repo
        //
        if(getRepo)
        {
            string repoFile = Path.Combine(backupDir, "repo.car");
            Logger.LogInfo($"(REPO)");
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
            List<string> blobs = BlueskyClient.ListBlobs(pds, did);
            string blobFile = Path.Combine(backupDir, "blobs.txt");
            Logger.LogInfo($"(BLOBS)");
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
                    Logger.LogTrace($"Getting blob file: {blobPath}");
                    BlueskyClient.GetBlob(pds, did, blob, blobPath);
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
        }
    }
}