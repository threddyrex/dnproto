
using dnproto.sdk.auth;
using dnproto.sdk.ws;
using dnproto.sdk.fs;

namespace dnproto.cli.commands;


public class GenerateKeyPair : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"dataDir"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"keytype"});
    }

    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
        string? keyType = CommandLineInterface.GetArgumentValue(arguments, "keytype");

        //
        // Load lfs
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);

        //
        // Generate key pair using reusable Key class
        //
        if(string.IsNullOrEmpty(keyType))
        {
            keyType = KeyTypes.P256; // default
        }
        KeyPair generatedKey = KeyPair.Generate(keyType);

        //
        // Display the key information
        //
        Logger.LogInfo("");
        Logger.LogInfo($"Key Type: {generatedKey.KeyTypeName}");
        Logger.LogInfo("");
        Logger.LogInfo("PRIVATE KEY: save this securely (eg, add to password manager)");
        Logger.LogInfo($"        (Multibase syntax) {generatedKey.PrivateKeyMultibase}");
        Logger.LogInfo($"        (Hex syntax) {generatedKey.PrivateKeyHex}");
        Logger.LogInfo("");
        Logger.LogInfo("PUBLIC KEY: share or publish this (eg, in DID document)");
        Logger.LogInfo($"        (DID Key Syntax) {generatedKey.DidKey}");
        Logger.LogInfo($"        (Multibase syntax) {generatedKey.PublicKeyMultibase}");
        Logger.LogInfo($"        (Hex syntax) {generatedKey.PublicKeyHex}");
        Logger.LogInfo("");
    }
}
