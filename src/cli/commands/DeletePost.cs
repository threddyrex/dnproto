using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.uri;
using dnproto.ws;

namespace dnproto.cli.commands;

public class DeletePost : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"dataDir", "handle", "url"});
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
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
        string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");
        string? url = CommandLineInterface.GetArgumentValue(arguments, "url");

        //
        // Load session
        //
        SessionFile? session = LocalFileSystem.Initialize(dataDir, Logger)?.LoadSession(handle);
        if (session == null)
        {
            Logger.LogError($"Failed to load session for handle: {handle}");
            return;
        }


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

        if (string.IsNullOrEmpty(uriOriginal.Authority) || string.IsNullOrEmpty(uriOriginal.Rkey))
        {
            Logger.LogError("Invalid URL format (missing authority or rkey).");
            return;
        }


        //
        // Get handle info
        //
        var handleInfo = BlueskyClient.ResolveHandleInfo(uriOriginal.Authority);


        //
        // Delete record
        //
        BlueskyClient.DeleteRecord(
            pds: session.pds,
            did: handleInfo.Did,
            rkey: uriOriginal.Rkey,
            accessJwt: session.accessJwt
        );

    }

}