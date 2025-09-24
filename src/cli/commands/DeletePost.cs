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
        return new HashSet<string>(new string[]{"url", "password"});
    }
    
    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"authFactorToken"});
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
        string? password = CommandLineInterface.GetArgumentValue(arguments, "password");
        string? authFactorToken = CommandLineInterface.GetArgumentValue(arguments, "authFactorToken");
        Logger.LogTrace($"url: {url}");

        if (string.IsNullOrEmpty(password))
        {
            Logger.LogError("Password is required.");
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
        Dictionary<string, string>? handleInfo = BlueskyClient.ResolveHandleInfo(uriOriginal.Authority);


        //
        // Login
        //
        var session = BlueskyClient.CreateSession(uriOriginal.Authority, password, authFactorToken);

        //
        // Delete record
        //
        BlueskyClient.DeleteRecord(
            pds: handleInfo["pds"],
            did: handleInfo["did"],
            rkey: uriOriginal.Rkey,
            accessJwt: JsonData.SelectString(session, "accessJwt")
        );

    }

}