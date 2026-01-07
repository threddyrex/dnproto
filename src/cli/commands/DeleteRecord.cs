using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.fs;
using dnproto.ws;


namespace dnproto.cli.commands;

public class DeleteRecord : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"actor", "collection", "rkey"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>([]);
    }


    /// <summary>
    /// Gets user profile.
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
        string? collection = CommandLineInterface.GetArgumentValue(arguments, "collection");
        string? rkey = CommandLineInterface.GetArgumentValue(arguments, "rkey");


        //
        // Load actor info and session
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
        var actorInfo = lfs?.ResolveActorInfo(actor);
        SessionFile? session = lfs?.LoadSession(actorInfo);
        if (session == null)
        {
            Logger.LogError($"Failed to load session for actor: {actor}. Please log in first.");
            return;
        }

        string accessJwt = session.accessJwt;
        string pds = session.pds;
        string did = session.did;


        //
        // Call pds
        //
        JsonNode requestBody = new JsonObject
        {
            ["repo"] = actor,
            ["collection"] = collection,
            ["rkey"] = rkey
        };
        
        string url = $"https://{pds}/xrpc/com.atproto.repo.deleteRecord";

        JsonNode? postResult = BlueskyClient.SendRequest(url,
            HttpMethod.Post,
            accessJwt: accessJwt,
            content: new StringContent(JsonSerializer.Serialize(requestBody)));

        BlueskyClient.PrintJsonResponseToConsole(postResult);
    }        
}