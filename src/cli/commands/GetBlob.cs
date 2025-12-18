using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.sdk.ws;
using dnproto.sdk.fs;

namespace dnproto.cli.commands;

/// <summary>
/// Get blobs for did.
/// </summary>
public class GetBlob : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "dataDir", "actor", "outdir" });
    }


    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get parameters
        //
        string? dataDir = arguments.ContainsKey("dataDir") ? arguments["dataDir"] : null;
        string? actor = arguments.ContainsKey("actor") ? arguments["actor"] : null;
        string? outdir = arguments.ContainsKey("outdir") ? arguments["outdir"] : null;

        if (string.IsNullOrEmpty(actor) || string.IsNullOrEmpty(outdir))
        {
            Logger.LogError("Missing required arguments.");
            return;
        }

        if (!Directory.Exists(outdir))
        {
            Logger.LogError($"Output directory does not exist: {outdir}");
            return;
        }

        //
        // Load lfs
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
        ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);


        //
        // Resolve handle
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
        string blobsFile = Path.Combine(outdir, $"blobs.json");
        List<string> blobs = BlueskyClient.ListBlobs(pds, did, blobsFile: blobsFile);

        //
        // Get blobs
        //
        foreach (var cid in blobs)
        {
            string blobFile = Path.Combine(outdir, $"{cid}");
            Logger.LogInfo($"Downloading blob: {cid} to {blobFile}");
            BlueskyClient.GetBlob(pds, did, cid?.ToString(), blobFile);
            System.Threading.Thread.Sleep(1000); // Throttle requests to avoid rate limiting
        }
    }
}