using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands;

public class GetPost : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"url"});
    }

    /// <summary>
    /// Print out the links for this post's parent, and also quoted post (if it exists).
    /// For viewing blocks.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? url = CommandLineInterface.GetArgumentValue(arguments, "url");
        Logger.LogTrace($"url: {url}");

        //
        // Parse to AtUri
        //
        var uriOriginal = AtUri.FromBskyPost(url);
        if (uriOriginal == null)
        {
            Logger.LogError("Invalid URL format.");
            return;
        }

        Logger.LogTrace("uriOriginal: " + uriOriginal.ToDebugString());

        if(string.IsNullOrEmpty(uriOriginal.Authority) || string.IsNullOrEmpty(uriOriginal.Rkey))
        {
            Logger.LogError("Invalid URL format (missing authority or rkey).");
            return;
        }

        //
        // If a handle was used, find the did.
        // The "getPosts" endpoint requires a did.
        //
        if(! uriOriginal.Authority.StartsWith("did:"))
        {
            string? did = BlueskyClient.ResolveHandleToDid_ViaBlueskyApi(uriOriginal.Authority);
            Logger.LogTrace($"did: {did}");

            if (string.IsNullOrEmpty(did))
            {
                Logger.LogError("Could not resolve handle to did.");
                return;
            }

            uriOriginal.Authority = did;
            Logger.LogTrace("uriOriginal: " + uriOriginal.ToDebugString());
        }


        //
        // construct AT URI
        //
        string atUri = uriOriginal.ToAtUri();
        Logger.LogTrace($"AT URI: {atUri}");


        //
        // call getPosts
        //
        string getPostsUrl = $"http://public.api.bsky.app/xrpc/app.bsky.feed.getPosts?uris={atUri}";
        Logger.LogTrace($"getPostsUrl: {getPostsUrl}");
        JsonNode? response = BlueskyClient.SendRequest(getPostsUrl, HttpMethod.Get);

        BlueskyClient.LogTraceJsonResponse(response);

        //
        // Find the quoted post.
        //
        string? quoteAtUri = response?["posts"]?[0]?["embed"]?["record"]?["uri"]?.ToString();
        string? quoteBskUrl = AtUri.FromAtUri(quoteAtUri)?.ToBskyPostUrl();

        if(!string.IsNullOrEmpty(quoteBskUrl))
        {
            Logger.LogInfo("QUOTE:");
            Logger.LogInfo($"{quoteAtUri}");
            Logger.LogInfo($"{quoteBskUrl}");
        }

        string? parentAtUri = response?["posts"]?[0]?["record"]?["reply"]?["parent"]?["uri"]?.ToString();
        string? parentBskUrl = AtUri.FromAtUri(parentAtUri)?.ToBskyPostUrl();

        if(!string.IsNullOrEmpty(parentBskUrl))
        {
            Logger.LogInfo("PARENT:");
            Logger.LogInfo($"{parentAtUri}");
            Logger.LogInfo($"{parentBskUrl}");
        }

        string? rootAtUri = response?["posts"]?[0]?["record"]?["reply"]?["root"]?["uri"]?.ToString();
        string? rootBskUrl = AtUri.FromAtUri(rootAtUri)?.ToBskyPostUrl();

        if(!string.IsNullOrEmpty(rootBskUrl))
        {
            Logger.LogInfo("ROOT:");
            Logger.LogInfo($"{rootAtUri}");
            Logger.LogInfo($"{rootBskUrl}");
        }

    }

}