using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.sdk.repo;
using dnproto.sdk.ws;
using dnproto.sdk.fs;

namespace dnproto.cli.commands;

public class Pds_GenerateDidWebDoc : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "did", "handle", "publicKeyMultibase", "pds" });
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[] { "datadir" });
    }

    /// <summary>
    /// Generate a DID Web document for the specified domain.
    /// </summary>
    /// <param name="arguments"></param>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? did = CommandLineInterface.GetArgumentValue(arguments, "did");
        string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");
        string? publicKeyMultibase = CommandLineInterface.GetArgumentValue(arguments, "publicKeyMultibase");
        string? pds = CommandLineInterface.GetArgumentValue(arguments, "pds");

        Logger.LogTrace($"did: {did}");
        Logger.LogTrace($"handle: {handle}");
        Logger.LogTrace($"publicKeyMultibase: {publicKeyMultibase}");
        Logger.LogTrace($"pds: {pds}");
        if (string.IsNullOrEmpty(did) || string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(publicKeyMultibase))
        {
            Logger.LogError("Required arguments missing. Need: did, pds, publicKeyMultibase");
            return;
        }

        // Ensure PDS has https:// prefix
        if (!pds.StartsWith("http://") && !pds.StartsWith("https://"))
        {
            pds = "https://" + pds;
        }

        //
        // Build the DID Web document
        //
        var didWebDoc = new
        {
            context = new[]
            {
                "https://www.w3.org/ns/did/v1",
                "https://w3id.org/security/multikey/v1",
                "https://w3id.org/security/suites/secp256k1-2019/v1"
            },
            id = $"{did}",
            alsoKnownAs = new[]
            {
                $"at://{handle}"
            },
            verificationMethod = new[]
            {
                new
                {
                    id = $"{did}#atproto",
                    type = "Multikey",
                    controller = $"{did}",
                    publicKeyMultibase = publicKeyMultibase
                }
            },
            service = new[]
            {
                new
                {
                    id = "#atproto_pds",
                    type = "AtprotoPersonalDataServer",
                    serviceEndpoint = pds
                }
            }
        };

        //
        // Serialize and output
        //
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Custom serialization to use @context instead of context
        var jsonString = JsonSerializer.Serialize(didWebDoc, options);
        jsonString = jsonString.Replace("\"context\":", "\"@context\":");

        // write to scratch dir
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "datadir");
        if (!string.IsNullOrEmpty(dataDir))
        {
            var lfs = LocalFileSystem.Initialize(dataDir, Logger);
            if (lfs != null)
            {
                string? scratchDir = lfs.GetPath_ScratchDir();
                if (!string.IsNullOrEmpty(scratchDir))
                {
                    string outputFile = Path.Combine(scratchDir, "did.json");
                    File.WriteAllText(outputFile, jsonString);
                    Logger.LogInfo($"Wrote DID Web document to: {outputFile}");
                }
            }
        }

        Logger.LogInfo("\n" + jsonString);
    }
}
