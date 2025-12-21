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

public class DescribeRepo : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"actor"});
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
        string url = $"https://{actorInfo.Pds}/xrpc/com.atproto.repo.describeRepo?repo={actorInfo.Did}";
        Logger.LogTrace($"url: {url}");
        JsonNode? response = BlueskyClient.SendRequest(url, HttpMethod.Get);
        BlueskyClient.PrintJsonResponseToConsole(response);
    }
}