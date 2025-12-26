using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands;

public class ViewPost : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"uri"});
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
        string? uri = CommandLineInterface.GetArgumentValue(arguments, "uri");
        Logger.LogTrace($"uri: {uri}");

        if (string.IsNullOrEmpty(uri))
        {
            Logger.LogError("URI argument is required.");
            return;
        }


        //
        // Figure out Bsky url
        //
        string? postUrl = null;

        if (uri.StartsWith("at://"))
        {
            postUrl = AtUri.FromAtUri(uri)?.ToBskyPostUrl();
            if (string.IsNullOrEmpty(postUrl))
            {
                Logger.LogError("Could not convert at:// URL to Bluesky URL.");
                return;
            }
        }
        else if (uri.Contains("bsky.app"))
        {
            postUrl = uri;
        }
        else
        {
            Logger.LogError("URI must be either an at:// URI or a bsky.app URI.");
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