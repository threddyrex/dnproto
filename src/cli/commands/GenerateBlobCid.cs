using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.fs;
using dnproto.ws;
using dnproto.repo;

namespace dnproto.cli.commands;

/// <summary>
/// Generate a CID for a blob file (image, video, etc).
/// </summary>
public class GenerateBlobCid : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "filepath" });
    }


    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get parameters
        //
        string? filepath = arguments.ContainsKey("filepath") ? arguments["filepath"] : null;

        if (string.IsNullOrEmpty(filepath))
        {
            Logger.LogError("Missing required argument: filepath.");
            return;
        }

        //
        // Check if file exists
        //
        if (!File.Exists(filepath))
        {
            Logger.LogError($"File not found: {filepath}");
            return;
        }

        //
        // Read the blob bytes
        //
        byte[] blobBytes;
        try
        {
            blobBytes = File.ReadAllBytes(filepath);
            Logger.LogInfo($"Read {blobBytes.Length} bytes from {filepath}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to read file: {ex.Message}");
            return;
        }

        //
        // Compute SHA-256 hash
        //
        CidV1 cid = CidV1.GenerateForBlobBytes(blobBytes);

        //
        // Output the result
        //
        Logger.LogInfo("");
        Logger.LogInfo($"Blob CID: {cid.Base32}");
        Logger.LogInfo($"Size: {blobBytes.Length} bytes");
        Logger.LogInfo($"SHA-256 hash: {BitConverter.ToString(cid.DigestBytes).Replace("-", "").ToLower()}");
        Logger.LogInfo("");
    }
}