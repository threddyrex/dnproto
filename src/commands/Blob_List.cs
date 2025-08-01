using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands;

/// <summary>
/// List blobs for did.
/// </summary>
public class Blob_List : BaseCommand
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
            Console.WriteLine("Missing required arguments.");
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
        JsonNode? blobsResponse = BlueskyUtils.ListBlobs(pds, did, blobsFile: outfile);

        WebServiceClient.PrintJsonResponseToConsole(blobsResponse);
    }
}