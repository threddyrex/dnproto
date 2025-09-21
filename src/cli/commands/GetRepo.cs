using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.ws;

namespace dnproto.cli.commands;

public class GetRepo : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"dataDir"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"handle", "pds", "did"});
    }


    /// <summary>
    /// Downloads a user's repository.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
        string? pds = CommandLineInterface.GetArgumentValue(arguments, "pds");
        string? did = CommandLineInterface.GetArgumentValue(arguments, "did");
        string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");

        Logger.LogTrace($"pds: {pds}");
        Logger.LogTrace($"did: {did}");
        Logger.LogTrace($"dataDir: {dataDir}");


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
        // If we're resolving handle, do that now.
        //
        if (string.IsNullOrEmpty(handle) == false)
        {
            Logger.LogTrace("Resolving handle to did.");
            Dictionary<string, string> handleInfo = BlueskyClient.ResolveHandleInfo(handle);

            did = handleInfo.ContainsKey("did") ? handleInfo["did"] : null;
            pds = handleInfo.ContainsKey("pds") ? handleInfo["pds"] : null;
        }

        Logger.LogTrace($"pds: {pds}");
        Logger.LogTrace($"did: {did}");

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did) || did.StartsWith("did:") == false)
        {
            Logger.LogError("Invalid arguments.");
            return;
        }

        string? repofile = localFileSystem.GetPath_RepoFile(handle ?? did);
        if (string.IsNullOrEmpty(repofile))
        {
            Logger.LogError("Failed to get repofile path.");
            return;
        }
        Logger.LogTrace($"repofile: {repofile}");


        //
        // Call pds
        //
        Logger.LogInfo($"Calling GetRepo with pds: {pds}, did: {did}, repofile: {repofile}");
        BlueskyClient.GetRepo(pds, did, repofile);
    }
}