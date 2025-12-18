using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.sdk.repo;
using dnproto.sdk.ws;
using dnproto.sdk.fs;

namespace dnproto.cli.commands;

public class GetPdsInfo : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"dataDir", "actor"});
    }



    /// <summary>
    /// Based on handle, get info for pds
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


        //
        // Load lfs
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
        ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

        if (actorInfo == null)
        {
            Logger.LogError("Failed to resolve actor info.");
            return;
        }


        //
        // Call pds
        //
        Logger.LogInfo($"Calling ListRepos.");
        Logger.LogInfo($"pds: {actorInfo.Pds}");

        JsonNode? response = null;

        Logger.LogInfo($"HEALTH");
        response = BlueskyClient.PdsHealth(actorInfo.Pds);
        BlueskyClient.PrintJsonResponseToConsole(response);

        Logger.LogInfo($"DESCRIBE SERVER");
        response = BlueskyClient.PdsDescribeServer(actorInfo.Pds);
        BlueskyClient.PrintJsonResponseToConsole(response);

        Logger.LogInfo($"LIST REPOS");
        List<JsonNode> repos = BlueskyClient.ListRepos(actorInfo.Pds);
        Logger.LogInfo($"repo count: {repos.Count}");
        foreach (JsonNode repo in repos)
        {
            BlueskyClient.PrintJsonResponseToConsole(repo);
        }
    }

}