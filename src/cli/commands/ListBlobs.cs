using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.fs;
using dnproto.ws;

namespace dnproto.cli.commands;

/// <summary>
/// List blobs for did.
/// </summary>
public class ListBlobs : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "actor" });
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[] {});
    }


    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get parameters
        //
        string? actor = arguments.ContainsKey("actor") ? arguments["actor"] : null;

        if (string.IsNullOrEmpty(actor))
        {
            Logger.LogError("Missing required arguments.");
            return;
        }


        //
        // Resolve actor
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
        // Get session (access token)
        //
        SessionFile? session = LocalFileSystem?.LoadSession(actorInfo);
        string? accessJwt = session?.accessJwt;

        if (string.IsNullOrEmpty(accessJwt))
        {
            Logger.LogInfo("Could not get access token. Calling without authentication.");
        }


        //
        // List blobs
        //
        List<string> blobs = BlueskyClient.ListBlobs(pds, did, accessJwt: accessJwt, limit: 100);

        foreach (var blob in blobs)
        {
            Logger.LogInfo($"Blob: {blob}");
        }

        Logger.LogInfo($"Total blobs: {blobs.Count}");
    }
}