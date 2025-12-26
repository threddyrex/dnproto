using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.fs;
using dnproto.ws;

namespace dnproto.cli.commands;

/// <summary>
/// Upload a blob.
/// </summary>
public class UploadBlob : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "actor", "filepath" });
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
        string? filePath = CommandLineInterface.GetArgumentValue(arguments, "filepath");

        if (string.IsNullOrEmpty(actor))
        {
            Logger.LogError("Missing actor.");
            return;
        }

        if (string.IsNullOrEmpty(filePath) || File.Exists(filePath) == false)
        {
            Logger.LogError("Missing filepath.");
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
            Logger.LogError("Could not get access token. Please create a session first.");
            return;
        }


        //
        // Infer MIME type from file
        //
        string? mimeType = LocalFileSystem?.InferMimeType(filePath);
        Logger.LogInfo($"Detected MIME type: {mimeType}");


        //
        // Read file content
        //
        byte[] fileBytes = File.ReadAllBytes(filePath);
        Logger.LogInfo($"File size: {fileBytes.Length} bytes");


        //
        // Upload blob
        //
        JsonNode? response = BlueskyClient.UploadBlob(pds, accessJwt, fileBytes, mimeType);

        if (response != null)
        {
            Logger.LogInfo("Blob uploaded successfully!");
            BlueskyClient.PrintJsonResponseToConsole(response);
            
            // Extract blob reference info
            var blobRef = response["blob"]?["ref"]?["$link"]?.ToString();
            var blobMimeType = response["blob"]?["mimeType"]?.ToString();
            var blobSize = response["blob"]?["size"]?.ToString();
            
            Logger.LogInfo($"Blob CID: {blobRef}");
            Logger.LogInfo($"MIME Type: {blobMimeType}");
            Logger.LogInfo($"Size: {blobSize} bytes");
        }
        else
        {
            Logger.LogError("Failed to upload blob.");
        }
    }
}