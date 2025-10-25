using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class LogIn : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"dataDir", "handle", "password"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"authFactorToken"});
    }


    /// <summary>
    /// Create session (log in)
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
        string? password = CommandLineInterface.GetArgumentValue(arguments, "password");
        string? authFactorToken = CommandLineInterface.GetArgumentValue(arguments, "authFactorToken");

        if (handle == null || password == null)
        {
            Logger.LogError("Missing required argument: handle or password");
            return;
        }


        //
        // Get local path of session file
        //
        string? sessionFile = LocalFileSystem.Initialize(dataDir, Logger)?.GetPath_SessionFile(handle);
        if (string.IsNullOrEmpty(sessionFile))
        {
            Logger.LogError($"Session file is null or empty: {sessionFile}");
            return;
        }


        //
        // Lookup handle info.
        //
        Logger.LogInfo("Resolving handle to get pds.");
        var handleInfo = BlueskyClient.ResolveHandleInfo(handle);
        string pds = string.IsNullOrEmpty(handleInfo.Pds) ? "bsky.social" : handleInfo.Pds;


        //
        // Construct url
        //
        string url = $"https://{pds}/xrpc/com.atproto.server.createSession";


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

        //
        // add pds
        //
        session["pds"] = pds;

        //
        // Write to disk
        //
        Logger.LogInfo($"Writing session file: {sessionFile}");
        JsonData.WriteJsonToFile(session, sessionFile);

    }
}