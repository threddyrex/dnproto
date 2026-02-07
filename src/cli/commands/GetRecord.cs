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
        return new HashSet<string>(new string[]{"uri"});
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
        string? uri = CommandLineInterface.GetArgumentValue(arguments, "uri");
        Logger.LogTrace($"uri: {uri}");

        //
        // Parse to AtUri
        //
        AtUri? uriOriginal = AtUri.FromBskyPost(uri);

        if(uriOriginal == null)
        {
            uriOriginal = AtUri.FromAtUri(uri);
        }

        if (uriOriginal == null)
        {
            Logger.LogError("Invalid URI format.");
            return;
        }

        Logger.LogTrace("uriOriginal: " + uriOriginal.ToDebugString());

        if(string.IsNullOrEmpty(uriOriginal.Authority) || string.IsNullOrEmpty(uriOriginal.Rkey))
        {
            Logger.LogError("Invalid URL format (missing authority or rkey).");
            return;
        }


        //
        // Load actor info and session
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
        var actorInfo = lfs?.ResolveActorInfo(uriOriginal.Authority);



        //
        // Call pds
        //
        string url = $"https://{actorInfo!.Pds}/xrpc/com.atproto.repo.getRecord?repo={uriOriginal.Authority}&collection={uriOriginal.Collection}&rkey={uriOriginal.Rkey}";
        JsonNode? response = BlueskyClient.SendRequest(url, HttpMethod.Get, null);

        //
        // Write to data directory
        //
        string filePath = lfs!.GetPath_Record(uriOriginal);
        string jsonString = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, jsonString);
        Logger.LogInfo($"Record saved to: {filePath}");
    }      
}