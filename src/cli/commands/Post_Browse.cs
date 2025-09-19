using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands;

public class Post_Browse : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"url"});
    }

    /// <summary>
    /// Browse a post in the system's default internet browser.
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

        if (string.IsNullOrEmpty(url))
        {
            Logger.LogError("URL argument is required.");
            return;
        }


        //
        // Figure out Bsky url
        //
        string? postUrl = null;

        if (url.StartsWith("at://"))
        {
            var uri = AtUri.FromAtUri(url);
            if (uri == null)
            {
                Logger.LogError("Invalid at:// URL format.");
                return;
            }

            postUrl = uri.ToBskyPostUrl();
            if (string.IsNullOrEmpty(postUrl))
            {
                Logger.LogError("Could not convert at:// URL to Bluesky URL.");
                return;
            }
        }
        else if (url.Contains("bsky.app"))
        {
            postUrl = url;
        }
        else
        {
            Logger.LogError("URL must be either an at:// URI or a bsky.app URL.");
            return;
        }


        //
        // Open in browser
        //
        Logger.LogInfo($"Opening in browser: {postUrl}");
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = postUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to open URL in browser: {ex.Message}");
        }

    }

}