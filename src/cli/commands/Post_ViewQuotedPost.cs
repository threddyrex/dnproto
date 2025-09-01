using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.ws;

namespace dnproto.cli.commands;

public class Post_ViewQuotedPost : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"url"});
    }

    /// <summary>
    /// Given a URL, find the post that it is quoting.
    /// Useful if the quoted post is blocked.
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
        // Find the embed. This is the quoted post.
        //
        string? embedUri = response?["posts"]?[0]?["embed"]?["record"]?["uri"]?.ToString();
        Console.WriteLine($"embedUri: {embedUri}");

        if (string.IsNullOrEmpty(embedUri))
        {
            Console.WriteLine("Could not find quoted post.");
            return;
        }

        //
        // Given the embedUri, construct the bsky url
        //
        string? didBlocked = embedUri?.Split('/')[2];
        Console.WriteLine($"didBlocked: {didBlocked}");

        string? postIdBlocked = embedUri?.Split('/')[4];
        Console.WriteLine($"postIdBlocked: {postIdBlocked}");

        string urlBlocked = $"https://bsky.app/profile/{didBlocked}/post/{postIdBlocked}";

        Console.WriteLine();
        Console.WriteLine($"!! CLICK HERE !!: {urlBlocked}");
        Console.WriteLine();

    }
}