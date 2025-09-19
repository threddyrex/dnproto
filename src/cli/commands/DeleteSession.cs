using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class Session_Delete : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"sessionFilePath"});
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
        string? sessionFilePath = CommandLineInterface.GetArgumentValue(arguments, "sessionFilePath");
        JsonNode? session = JsonData.ReadJsonFromFile(sessionFilePath);
        string? refreshJwt = JsonData.SelectString(session, "refreshJwt");
        string? pds = JsonData.SelectString(session, "pds");
        string? did = JsonData.SelectString(session, "did");
        string url = $"https://{pds}/xrpc/com.atproto.server.deleteSession";

        Logger.LogInfo($"pds: {pds}");
        Logger.LogInfo($"did: {did}");
        Logger.LogInfo($"url: {url}");

        //
        // Clear local state
        //
        if(File.Exists(sessionFilePath))
        {
            Logger.LogInfo($"Deleting session file: {sessionFilePath}");
            File.Delete(sessionFilePath);
        }

        //
        // Call the API.
        // (You can't delete accessJwt, but you can delete refreshJwt.)
        //
        if (string.IsNullOrEmpty(refreshJwt))
        {
            Logger.LogError("Session not found. Nothing to delete.");
            return;
        }

        JsonNode? response = BlueskyClient.SendRequest(url,
            HttpMethod.Post, 
            accessJwt: refreshJwt);


        //
        // Print results
        //
        BlueskyClient.PrintJsonResponseToConsole(response);
    }
}