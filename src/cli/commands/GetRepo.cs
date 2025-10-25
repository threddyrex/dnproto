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
        return new HashSet<string>(new string[]{"dataDir", "actor"});
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
        string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");

        Logger.LogTrace($"dataDir: {dataDir}");
        Logger.LogTrace($"actor: {actor}");


        // resolve handle
        var handleInfo = BlueskyClient.ResolveHandleInfo(actor);


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
        Logger.LogTrace($"pds: {handleInfo.Pds}");
        Logger.LogTrace($"did: {handleInfo.Did}");

        if (string.IsNullOrEmpty(handleInfo.Pds) || string.IsNullOrEmpty(handleInfo.Did) || handleInfo.Did.StartsWith("did:") == false)
        {
            Logger.LogError("Invalid arguments.");
            return;
        }

        string? repofile = localFileSystem.GetPath_RepoFile(handleInfo);
        if (string.IsNullOrEmpty(repofile))
        {
            Logger.LogError("Failed to get repofile path.");
            return;
        }
        Logger.LogTrace($"repofile: {repofile}");


        //
        // Call pds
        //
        Logger.LogInfo($"Calling GetRepo.");
        Logger.LogInfo($"pds: {handleInfo.Pds}");
        Logger.LogInfo($"did: {handleInfo.Did}");
        Logger.LogInfo($"Writing repofile: {repofile}");
        BlueskyClient.GetRepo(handleInfo.Pds, handleInfo.Did, repofile);
    }
}