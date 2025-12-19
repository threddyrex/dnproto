namespace dnproto.cli.commands;


public class SignToken : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"dataDir", "publickey", "privatekey", "issuer", "audience"});
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
        var signer = new dnproto.sdk.key.Signer(publicKey, privateKey, issuer, audience);
        string token = signer.SignToken();
        Logger.LogInfo($"Signed Token:\n{token}");
    }
}
