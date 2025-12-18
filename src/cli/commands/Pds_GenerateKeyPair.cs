
using dnproto.sdk.key;
using dnproto.sdk.ws;
using dnproto.sdk.fs;

namespace dnproto.cli.commands;


public class GenerateKey : BaseCommand
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
        Key generatedKey = Key.Generate(keyType);

        //
        // Display the key information
        //
        Logger.LogInfo("");
        Logger.LogInfo($"Key Type: {generatedKey.KeyTypeName}");
        Logger.LogInfo("");
        Logger.LogInfo("Secret Key (Multibase Syntax): save this securely (eg, add to password manager)");
        Logger.LogInfo($"        {generatedKey.PrivateKeyMultibase}");
        Logger.LogInfo("");
        Logger.LogInfo("Public Key (DID Key Syntax): share or publish this (eg, in DID document)");
        Logger.LogInfo($"        {generatedKey.DidKey}");
        Logger.LogInfo("");
    }
}
