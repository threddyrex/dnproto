using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class Session_Create : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"handle", "password", "sessionFile"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"pds", "authFactorToken"});
    }


    /// <summary>
    /// Create session (log in)
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        // Get arguments
        string? pds = arguments.ContainsKey("pds") ? arguments["pds"] : null;
        string? authFactorToken = CommandLineInterface.GetArgumentValue(arguments, "authFactorToken");
        string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");
        string? password = CommandLineInterface.GetArgumentValue(arguments, "password");

        if(handle == null || password == null)
        {
            Logger.LogError("Missing required argument: handle or password");
            return;
        }

        // If pds is not provided, get it from the handle
        if (string.IsNullOrEmpty(pds))
        {
            Logger.LogInfo("Resolving handle to get pds.");
            var handleInfo = BlueskyClient.ResolveHandleInfo(handle);
            pds = handleInfo.ContainsKey("pds") ? handleInfo["pds"] : "bsky.social";
        }

        // Construct url
        string url = $"https://{pds}/xrpc/com.atproto.server.createSession";
        Logger.LogInfo($"url: {url}");


        //
        // Send request
        //
        JsonNode? session = BlueskyClient.SendRequest(url,
            HttpMethod.Post,
            content: string.IsNullOrEmpty(authFactorToken) ?
                new StringContent(JsonSerializer.Serialize(new
                    {
                        identifier = handle,
                        password = password
                    })) : 
                new StringContent(JsonSerializer.Serialize(new
                    {
                        identifier = handle,
                        password = password,
                        authFactorToken = authFactorToken
                    }))
        );

        if (session == null)
        {
            Logger.LogError("Session returned null.");
            return;
        }

        // add pds
        session["pds"] = pds;

        //
        // Process response
        //
        BlueskyClient.PrintJsonResponseToConsole(session);
        JsonData.WriteJsonToFile(session, CommandLineInterface.GetArgumentValue(arguments, "sessionFile"));

    }
}