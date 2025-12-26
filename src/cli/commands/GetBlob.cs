using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.fs;
using dnproto.ws;

namespace dnproto.cli.commands;

/// <summary>
/// Get blobs for did.
/// </summary>
public class GetBlob : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "actor", "cid" });
    }


    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get parameters
        //
        string? actor = arguments.ContainsKey("actor") ? arguments["actor"] : null;
        string? cid = arguments.ContainsKey("cid") ? arguments["cid"] : null;

        if (string.IsNullOrEmpty(actor) || string.IsNullOrEmpty(cid))
        {
            Logger.LogError("Missing required arguments.");
            return;
        }


        //
        // Resolve handle
        //
        ActorInfo? actorInfo = LocalFileSystem?.ResolveActorInfo(actor);
        string? pds = actorInfo?.Pds;
        string? did = actorInfo?.Did;

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did))
        {
            Logger.LogError("Could not resolve PDS or DID for the actor.");
            return;
        }


        //
        // Get blob
        //
        string outputBlobFile = Path.Combine(LocalFileSystem?.GetPath_ScratchDir()!, cid);
        Logger.LogInfo($"Downloading blob to: {outputBlobFile}");
        string url = $"https://{pds}/xrpc/com.atproto.sync.getBlob?did={did}&cid={cid}";
        BlueskyClient.SendRequest(url, HttpMethod.Get, parseJsonResponse: false, outputFilePath: outputBlobFile);
    }
}