using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.ws;

namespace dnproto.cli.commands;

/// <summary>
/// List blobs for did.
/// </summary>
public class GetBlobList : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "handle" });
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
        string? handle = arguments.ContainsKey("handle") ? arguments["handle"] : null;
        string? outfile = arguments.ContainsKey("outfile") ? arguments["outfile"] : null;

        if (string.IsNullOrEmpty(handle))
        {
            Logger.LogError("Missing required arguments.");
            return;
        }


        //
        // Resolve handle
        //
        Dictionary<string, string> handleInfo = BlueskyClient.ResolveHandleInfo(handle);
        string? pds = handleInfo.ContainsKey("pds") ? handleInfo["pds"] : null;
        string? did = handleInfo.ContainsKey("did") ? handleInfo["did"] : null;

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did))
        {
            Logger.LogError("Could not resolve PDS or DID for the handle.");
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