using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands;

public class Post_ViewBlock : BaseCommand
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
        Console.WriteLine($"url: {url}");

        //
        // Parse to AtUri
        //
        var uriOriginal = AtUri.FromBskyPost(url);
        if (uriOriginal == null)
        {
            Console.WriteLine("Invalid URL format.");
            return;
        }

        Console.WriteLine("uriOriginal: " + uriOriginal.ToDebugString());

        if(string.IsNullOrEmpty(uriOriginal.Authority) || string.IsNullOrEmpty(uriOriginal.Rkey))
        {
            Console.WriteLine("Invalid URL format (missing authority or rkey).");
            return;
        }

        //
        // If a handle was used, find the did.
        // The "getPosts" endpoint requires a did.
        //
        if(! uriOriginal.Authority.StartsWith("did:"))
        {
            string? did = BlueskyClient.ResolveHandleToDid(uriOriginal.Authority);
            Console.WriteLine($"did: {did}");

            if (string.IsNullOrEmpty(did))
            {
                Console.WriteLine("Could not resolve handle to did.");
                return;
            }

            uriOriginal.Authority = did;
            Console.WriteLine("uriOriginal: " + uriOriginal.ToDebugString());
        }


        //
        // construct AT URI
        //
        string atUri = uriOriginal.ToAtUri();
        Console.WriteLine($"AT URI: {atUri}");


        //
        // call getPosts
        //
        string getPostsUrl = $"http://public.api.bsky.app/xrpc/app.bsky.feed.getPosts?uris={atUri}";
        Console.WriteLine($"getPostsUrl: {getPostsUrl}");
        JsonNode? response = BlueskyClient.SendRequest(getPostsUrl, HttpMethod.Get);
        BlueskyClient.PrintJsonResponseToConsole(response);


        //
        // Find the quoted post.
        //
        string? quoteAtUri = response?["posts"]?[0]?["embed"]?["record"]?["uri"]?.ToString();
        string? quoteBskUrl = AtUri.FromAtUri(quoteAtUri)?.ToBskyPostUrl();

        if(!string.IsNullOrEmpty(quoteBskUrl))
        {
            Console.WriteLine("QUOTE:");
            Console.WriteLine($"    {quoteAtUri}");
            Console.WriteLine($"    {quoteBskUrl}");
            Console.WriteLine();
        }

        string? parentAtUri = response?["posts"]?[0]?["record"]?["reply"]?["parent"]?["uri"]?.ToString();
        string? parentBskUrl = AtUri.FromAtUri(parentAtUri)?.ToBskyPostUrl();

        if(!string.IsNullOrEmpty(parentBskUrl))
        {
            Console.WriteLine("PARENT:");
            Console.WriteLine($"    {parentAtUri}");
            Console.WriteLine($"    {parentBskUrl}");
            Console.WriteLine();
        }

        string? rootAtUri = response?["posts"]?[0]?["record"]?["reply"]?["root"]?["uri"]?.ToString();
        string? rootBskUrl = AtUri.FromAtUri(rootAtUri)?.ToBskyPostUrl();

        if(!string.IsNullOrEmpty(rootBskUrl))
        {
            Console.WriteLine("ROOT:");
            Console.WriteLine($"    {rootAtUri}");
            Console.WriteLine($"    {rootBskUrl}");
            Console.WriteLine();
        }

    }

}