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
        return new HashSet<string>(new string[]{"uri"});
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
        string? uri = CommandLineInterface.GetArgumentValue(arguments, "uri");
        Logger.LogTrace($"uri: {uri}");

        //
        // Parse to AtUri
        //
        AtUri? uriOriginal = AtUri.FromBskyPost(uri);

        if(uriOriginal == null)
        {
            uriOriginal = AtUri.FromAtUri(uri);
        }

        if (uriOriginal == null)
        {
            Logger.LogError("Invalid URI format.");
            return;
        }

        Logger.LogTrace("uriOriginal: " + uriOriginal.ToDebugString());

        if(string.IsNullOrEmpty(uriOriginal.Authority) || string.IsNullOrEmpty(uriOriginal.Rkey))
        {
            Logger.LogError("Invalid URL format (missing authority or rkey).");
            return;
        }

        //
        // make sure did
        //
        ActorInfo? actorInfo = BlueskyClient.ResolveActorInfo(uriOriginal.Authority);
        uriOriginal.Authority = actorInfo.Did;
        if (string.IsNullOrEmpty(actorInfo.Did))
        {
            Logger.LogError("Could not resolve handle to did.");
            return;
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
        JsonNode? response = BlueskyClient.SendRequest(getPostsUrl, HttpMethod.Get, labelers: string.Join(',', GetProfile.GetLabelers()));

        BlueskyClient.LogTraceJsonResponse(response);


        // loop through entire jsonnode response and print out any item that is "uri"
        Logger.LogInfo("All URIs found in response:");
        Logger.LogInfo("");
        FindAndPrintUris(response);
    }


    /// <summary>
    /// Recursively traverse a JsonNode and print all properties named "uri"
    /// </summary>
    private void FindAndPrintUris(JsonNode? node, string path = "")
    {
        if (node == null)
            return;

        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                string currentPath = string.IsNullOrEmpty(path) ? property.Key : $"{path}.{property.Key}";
                
                if (property.Key.Equals("uri", StringComparison.OrdinalIgnoreCase))
                {
                    string? url = AtUri.FromAtUri(property.Value?.ToString())?.ToBskyPostUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        Logger.LogInfo(currentPath);
                        Logger.LogInfo($"{url}");
                        Logger.LogInfo("");
                    }
                }
                
                // Recursively check the property value
                FindAndPrintUris(property.Value, currentPath);
            }
        }
        else if (node is JsonArray array)
        {
            for (int i = 0; i < array.Count; i++)
            {
                string currentPath = $"{path}[{i}]";
                FindAndPrintUris(array[i], currentPath);
            }
        }
    }

}
