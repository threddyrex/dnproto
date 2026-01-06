using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.fs;
using dnproto.repo;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands;

public class GetRecord : BaseCommand
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



        //
        // Call pds
        //
        string url = $"https://{actorInfo!.Pds}/xrpc/com.atproto.repo.getRecord?repo={actor}&collection={collection}&rkey={rkey}";
        JsonNode? response = BlueskyClient.SendRequest(url, HttpMethod.Get, null);

        BlueskyClient.PrintJsonResponseToConsole(response);
    }        
}