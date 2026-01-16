using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;
using dnproto.fs;

namespace dnproto.cli.commands;

public class CreateSession : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"actor"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"password", "authFactorToken"});
    }


    /// <summary>
    /// Create session (log in)
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    [RequiresUnreferencedCode("PublishTrimmmed is false")]
    [RequiresDynamicCode("PublishTrimmmed is false")]
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
        string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");
        string? password = CommandLineInterface.GetArgumentValue(arguments, "password");
        string? authFactorToken = CommandLineInterface.GetArgumentValue(arguments, "authFactorToken");

        if (string.IsNullOrEmpty(actor))
        {
            Logger.LogError("Missing required argument: actor");
            return;
        }

        //
        // Ask for password if not provided
        //
        if (string.IsNullOrEmpty(password))
        {
            Logger.LogInfo($"Actor is {actor}.");
            Console.Write($"please enter password:");
            password = Console.ReadLine();
        }

        //
        // Load actor
        //
        LocalFileSystem? lfs = this.LocalFileSystem;
        ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

        Logger.LogInfo("Resolving handle to get pds.");
        string? pds = string.IsNullOrEmpty(actorInfo?.Pds) ? "bsky.social" : actorInfo?.Pds;
        if (string.IsNullOrEmpty(pds))
        {
            Logger.LogError("Failed to resolve pds.");
            return;
        }


        //
        // Get local path of session file
        //
        string? sessionFile = lfs?.GetPath_SessionFile(actorInfo);
        if (string.IsNullOrEmpty(sessionFile))
        {
            Logger.LogError($"Session file is null or empty: {sessionFile}");
            return;
        }


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
                        identifier = actor,
                        password = password
                    })) : 
                new StringContent(JsonSerializer.Serialize(new
                    {
                        identifier = actor,
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

        BlueskyClient.PrintJsonResponseToConsole(session);

    }
}