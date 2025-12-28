using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.fs;
using dnproto.ws;

namespace dnproto.cli.commands;

/// <summary>
/// Get blobs for did.
/// </summary>
public class SearchActors : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "actor", "query" });
    }


    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get parameters
        //
        string? query = arguments.ContainsKey("query") ? arguments["query"] : null;
        string? actor = arguments.ContainsKey("actor") ? arguments["actor"] : null;

        if (string.IsNullOrEmpty(query))
        {
            Logger.LogError("Missing required arguments.");
            return;
        }

        //
        // Get actor
        //
        ActorInfo? actorInfo = LocalFileSystem?.ResolveActorInfo(actor);
        SessionFile? session = LocalFileSystem?.LoadSession(actorInfo);


        //
        // Run search
        //
        string url = $"https://{actorInfo?.Pds}/xrpc/app.bsky.actor.searchActors?q={query}";
        JsonNode? response = BlueskyClient.SendRequest(url, HttpMethod.Get, parseJsonResponse: true, accessJwt: session?.accessJwt);
        BlueskyClient.PrintJsonResponseToConsole(response);

    }
}