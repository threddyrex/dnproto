using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class Account_Backup : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"handle", "password", "outputDir"});
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
        string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");
        string? password = CommandLineInterface.GetArgumentValue(arguments, "password");
        string? outputDir = CommandLineInterface.GetArgumentValue(arguments, "outputDir");
        string? authFactorToken = CommandLineInterface.GetArgumentValue(arguments, "authFactorToken");
        bool getPrefs = CommandLineInterface.GetArgumentValueWithDefault(arguments, "getprefs", true);
        bool getRepo = CommandLineInterface.GetArgumentValueWithDefault(arguments, "getrepo", true);
        bool getBlobs = CommandLineInterface.GetArgumentValueWithDefault(arguments, "getblobs", true);
        int blobSleepSeconds = CommandLineInterface.GetArgumentValueWithDefault(arguments, "blobsleepseconds", 1);

        Console.WriteLine($"handle: {handle}");
        Console.WriteLine($"outputDir: {outputDir}");
        Console.WriteLine($"getPrefs: {getPrefs}");
        Console.WriteLine($"getRepo: {getRepo}");
        Console.WriteLine($"getBlobs: {getBlobs}");
        Console.WriteLine($"password length: {password?.Length}");
        Console.WriteLine($"authFactorToken length: {authFactorToken?.Length}");

        if(string.IsNullOrEmpty(outputDir) || string.IsNullOrEmpty(handle) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Missing required argument.");
            return;
        }

        if(!Directory.Exists(outputDir))
        {
            Console.WriteLine("Output directory does not exist.");
            return;
        }


        //
        // Get the current date and time in a format suitable for a Windows directory
        //
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string backupDir = Path.Combine(outputDir, $"{handle}_{timestamp}");
        Console.WriteLine($"Creating backup directory: {backupDir}");
        Directory.CreateDirectory(backupDir);

        if(!Directory.Exists(backupDir))
        {
            Console.WriteLine("Failed to create backup directory.");
            return;
        }


        //
        // Create readme.txt
        //
        string readmePath = Path.Combine(backupDir, "README.txt");
        string readmeContents = "Account backup for Bluesky account. Created by dnproto.\n\n";
        readmeContents += $"handle: {handle}\n";
        readmeContents += $"outputDir: {outputDir}\n";
        readmeContents += $"backupDir: {backupDir}\n";
        readmeContents += $"getPrefs: {getPrefs}\n";
        readmeContents += $"getRepo: {getRepo}\n";
        readmeContents += $"getBlobs: {getBlobs}\n";
        readmeContents += $"password length: {password?.Length}\n";
        readmeContents += $"authFactorToken length: {authFactorToken?.Length}\n";
        Console.WriteLine($"Creating readme file: {readmePath}");
        File.WriteAllText(readmePath, readmeContents);

        if(!File.Exists(readmePath))
        {
            Console.WriteLine("Failed to create readme file.");
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
            Console.WriteLine("Failed to resolve handle to did and pds.");
            return;
        }


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
                Console.WriteLine("FAILED TO CREATE SESSION! Either handle/password is incorrect, or you need to provide the authFactor token from email.");
                return;
            }
            else
            {
                Console.WriteLine($"Successfully created session.");
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
            Console.WriteLine($"Creating prefs file: {prefsFile}");
            JsonData.WriteJsonToFile(response, prefsFile);

        }


        //
        // Get repo
        //
        if(getRepo)
        {
            string repoFile = Path.Combine(backupDir, "repo.car");
            Console.WriteLine($"Getting repo file: {repoFile}");
            BlueskyClient.GetRepo(pds, did, repoFile);
        }



        //
        // Get blobs
        //
        if(getBlobs)
        {
            //
            // List blobs (this just gives you the blob IDs)
            //
            List<string> blobs = BlueskyClient.ListBlobs(pds, did);
            string blobFile = Path.Combine(backupDir, "blobs.txt");
            Console.WriteLine($"Creating blob file: {blobFile}");
            File.WriteAllLines(blobFile, blobs);


            //
            // Create blobs directory
            //
            string blobsDirectory = Path.Combine(backupDir, "blobs");
            Console.WriteLine($"Creating blobs directory: {blobsDirectory}");
            Directory.CreateDirectory(blobsDirectory);

            if(!Directory.Exists(blobsDirectory))
            {
                Console.WriteLine("Failed to create blobs directory.");
                return;
            }


            //
            // Get all blobs
            //
            foreach(string blob in blobs)
            {
                string blobPath = Path.Combine(blobsDirectory, blob);
                Console.WriteLine($"Getting blob file: {blobPath}");
                BlueskyClient.GetBlob(pds, did, blob, blobPath);
                Thread.Sleep(blobSleepSeconds * 1000);
            }
        }
    }
}