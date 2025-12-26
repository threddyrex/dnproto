
using dnproto.auth;

namespace dnproto.cli.commands;


public class SignToken : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"publickey", "privatekey", "issuer", "audience"});
    }


    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
        string? publicKey = CommandLineInterface.GetArgumentValue(arguments, "publickey");
        string? privateKey = CommandLineInterface.GetArgumentValue(arguments, "privatekey");
        string? issuer = CommandLineInterface.GetArgumentValue(arguments, "issuer");
        string? audience = CommandLineInterface.GetArgumentValue(arguments, "audience");
        if (dataDir == null || publicKey == null || privateKey == null || issuer == null || audience == null)
        {
            Logger.LogError("Missing required arguments.");
            return;
        }

        //
        // Call signer
        //
        string token = Signer.SignToken(publicKey, privateKey, issuer, audience);
        Logger.LogInfo($"Signed Token:\n{token}");
    }
}
