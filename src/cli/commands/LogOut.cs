using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class LogOut : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"dataDir", "actor"});
    }
    
    /// <summary>
    /// Delete session. At the moment, you can't call this with the accessJwt - that
    /// one needs to expire on the server on its own. But you can delete the refreshJwt.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
        string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");

        //
        // Load lfs
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
        ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

        //
        // Load session
        //
        SessionFile? session = lfs?.LoadSession(actorInfo);
        if (session == null)
        {
            Logger.LogError($"Failed to load session for actor: {actor}");
            return;
        }



        string url = $"https://{session.pds}/xrpc/com.atproto.server.deleteSession";

        Logger.LogInfo($"pds: {session.pds}");
        Logger.LogInfo($"did: {session.did}");
        Logger.LogInfo($"url: {url}");

        //
        // Clear local state
        //
        if(File.Exists(session.filePath))
        {
            Logger.LogInfo($"Deleting session file: {session.filePath}");
            File.Delete(session.filePath);
        }

        //
        // Call the API.
        // (You can't delete accessJwt, but you can delete refreshJwt.)
        //
        if (string.IsNullOrEmpty(session.refreshJwt))
        {
            Logger.LogError("Session not found. Nothing to delete.");
            return;
        }

        JsonNode? response = BlueskyClient.SendRequest(url,
            HttpMethod.Post, 
            accessJwt: session.refreshJwt);


        //
        // Print results
        //
        BlueskyClient.PrintJsonResponseToConsole(response);
    }
}