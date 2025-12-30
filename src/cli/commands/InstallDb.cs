

using dnproto.pds;
using dnproto.pds.db;


namespace dnproto.cli.commands
{
    /// <summary>
    /// A minimal ASP.NET Core web API that responds to /hello with "world"
    /// </summary>
    public class InstallDb : BaseCommand
    {
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get Arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");

            //
            // Initialize PDS
            //
            PdsDb.InstallPdsDb(dataDir!, Logger, force: true);
        }
    }
}
