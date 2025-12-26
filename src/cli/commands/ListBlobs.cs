using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.fs;
using dnproto.ws;

namespace dnproto.cli.commands;

/// <summary>
/// List blobs for did.
/// </summary>
public class GetBlobList : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "actor" });
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[] { "outfile" });
    }


    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get parameters
        //
        string? dataDir = arguments.ContainsKey("dataDir") ? arguments["dataDir"] : null;
        string? actor = arguments.ContainsKey("actor") ? arguments["actor"] : null;
        string? outfile = arguments.ContainsKey("outfile") ? arguments["outfile"] : null;

        if (string.IsNullOrEmpty(actor))
        {
            Logger.LogError("Missing required arguments.");
            return;
        }

        //
        // Load lfs
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
        ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

        //
        // Resolve actor
        //
        string? pds = actorInfo?.Pds;
        string? did = actorInfo?.Did;

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did))
        {
            Logger.LogError("Could not resolve PDS or DID for the actor.");
            return;
        }



        //
        // List blobs
        //
        List<string> blobs = BlueskyClient.ListBlobs(pds, did, limit: 100);

        foreach (var blob in blobs)
        {
            Logger.LogInfo($"Blob: {blob}");
        }

        if (outfile != null)
        {
            File.WriteAllLines(outfile, blobs);
            Logger.LogInfo($"Blobs written to {outfile}");
        }

        Logger.LogInfo($"Total blobs: {blobs.Count}");
    }
}