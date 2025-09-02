using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.ws;

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
        // split the url by the '/' character and find both the username and the post id
        //
        string[] urlParts = url?.Split('/') ?? Array.Empty<string>();
        string? username = urlParts.Length > 4 ? urlParts[4] : null;
        string? postId = urlParts.Length > 6 ? urlParts[6] : null;

        Console.WriteLine($"username: {username}");
        Console.WriteLine($"postId: {postId}");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(postId))
        {
            Console.WriteLine("Invalid URL format.");
            return;
        }


        //
        // Resolve handle to did
        //
        string? did = BlueskyClient.ResolveHandleToDid(username);
        Console.WriteLine($"did: {did}");

        if (string.IsNullOrEmpty(did))
        {
            Console.WriteLine("Could not resolve handle to did.");
            return;
        }


        //
        // construct AT URL
        //
        string atUrl = $"at://{did}/app.bsky.feed.post/{postId}";
        Console.WriteLine($"AT URL: {atUrl}");


        //
        // call getPosts
        //
        string getPostsUrl = $"http://public.api.bsky.app/xrpc/app.bsky.feed.getPosts?uris={atUrl}";
        Console.WriteLine($"getPostsUrl: {getPostsUrl}");
        JsonNode? response = BlueskyClient.SendRequest(getPostsUrl, HttpMethod.Get);
        BlueskyClient.PrintJsonResponseToConsole(response);


        //
        // Find the quoted post.
        //
        string? quoteAtUrl = response?["posts"]?[0]?["embed"]?["record"]?["uri"]?.ToString();
        string? quoteBskUrl = ConvertAtUrlToBskyUrl(quoteAtUrl);

        if(!string.IsNullOrEmpty(quoteBskUrl))
        {
            Console.WriteLine("QUOTE:");
            Console.WriteLine($"    {quoteAtUrl}");
            Console.WriteLine($"    {quoteBskUrl}");
            Console.WriteLine();
        }

        string? parentAtUrl = response?["posts"]?[0]?["record"]?["reply"]?["parent"]?["uri"]?.ToString();
        string? parentBskUrl = ConvertAtUrlToBskyUrl(parentAtUrl);

        if(!string.IsNullOrEmpty(parentBskUrl))
        {
            Console.WriteLine("PARENT:");
            Console.WriteLine($"    {parentAtUrl}");
            Console.WriteLine($"    {parentBskUrl}");
            Console.WriteLine();
        }

        string? rootAtUrl = response?["posts"]?[0]?["record"]?["reply"]?["root"]?["uri"]?.ToString();
        string? rootBskUrl = ConvertAtUrlToBskyUrl(rootAtUrl);

        if(!string.IsNullOrEmpty(rootBskUrl))
        {
            Console.WriteLine("ROOT:");
            Console.WriteLine($"    {rootAtUrl}");
            Console.WriteLine($"    {rootBskUrl}");
            Console.WriteLine();
        }

    }


    public static string? ConvertAtUrlToBskyUrl(string? atUrl)
    {
        if (string.IsNullOrEmpty(atUrl))
        {
            return null;
        }

        // Extract the DID and post ID from the AT URL
        string[] parts = atUrl.Split('/');
        if (parts.Length < 5)
        {
            return null;
        }

        string did = parts[2];
        string postId = parts[4];

        // Construct the BSky URL
        return $"https://bsky.app/profile/{did}/post/{postId}";
    }
}