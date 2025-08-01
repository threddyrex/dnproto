using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands;

/// <summary>
/// Get blobs for did.
/// </summary>
public class Blob_Get : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "handle", "outdir" });
    }


    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get parameters
        //
        string? handle = arguments.ContainsKey("handle") ? arguments["handle"] : null;
        string? outdir = arguments.ContainsKey("outdir") ? arguments["outdir"] : null;

        if (string.IsNullOrEmpty(handle) || string.IsNullOrEmpty(outdir))
        {
            Console.WriteLine("Missing required arguments.");
            return;
        }

        if (!Directory.Exists(outdir))
        {
            Console.WriteLine($"Output directory does not exist: {outdir}");
            return;
        }



        //
        // Resolve handle
        //
        Dictionary<string, string> handleInfo = BlueskyUtils.ResolveHandleInfo(handle);
        string? pds = handleInfo.ContainsKey("pds") ? handleInfo["pds"] : null;
        string? did = handleInfo.ContainsKey("did") ? handleInfo["did"] : null;

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did))
        {
            Console.WriteLine("Could not resolve PDS or DID for the handle.");
            return;
        }

        CommandLineInterface.PrintLineSeparator();


        //
        // List blobs
        //
        string blobsFile = Path.Combine(outdir, $"blobs.json");
        List<string> blobs = BlueskyUtils.ListBlobs(pds, did, blobsFile: blobsFile);

        //
        // Get blobs
        //
        foreach (var cid in blobs)
        {
            string blobFile = Path.Combine(outdir, $"{cid}");
            Console.WriteLine($"Downloading blob: {cid} to {blobFile}");
            BlueskyUtils.GetBlob(pds, did, cid?.ToString(), blobFile);
            System.Threading.Thread.Sleep(1000); // Throttle requests to avoid rate limiting
        }
    }
}