
using dnproto.auth;
using dnproto.ws;
using dnproto.fs;

namespace dnproto.cli.commands;


public class GenerateJwtSecret : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{});
    }
    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{});
    }

    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");

        //
        // Generate key pair using reusable Key class
        //
        string? jwtSecret = JwtSecret.GenerateJwtSecret();

        //
        // Display the key information
        //
        Logger.LogInfo("");
        Logger.LogInfo($"JWT Secret: {jwtSecret}");
        Logger.LogInfo("");
    }
}
