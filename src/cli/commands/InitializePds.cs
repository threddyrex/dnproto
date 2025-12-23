

using dnproto.pds;
using dnproto.pds.db;
using dnproto.sdk.auth;


namespace dnproto.cli.commands
{
    /// <summary>
    /// A minimal ASP.NET Core web API that responds to /hello with "world"
    /// </summary>
    public class InitializePds : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"pdshostname", "availableuserdomain", "userHandle", "userDid", "userEmail"});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get Arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? pdsHostname = CommandLineInterface.GetArgumentValue(arguments, "pdshostname");
            string? availableUserDomain = CommandLineInterface.GetArgumentValue(arguments, "availableuserdomain");
            string? userHandle = CommandLineInterface.GetArgumentValue(arguments, "userHandle");
            string? userDid = CommandLineInterface.GetArgumentValue(arguments, "userDid");
            string? userEmail = CommandLineInterface.GetArgumentValue(arguments, "userEmail");

            //
            // Initialize PDS
            //
            Pds.InitializePds(Logger, dataDir, pdsHostname, availableUserDomain, userHandle, userDid, userEmail);
        }
    }
}
