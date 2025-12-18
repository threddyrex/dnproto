using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using dnproto.pds;
using dnproto.sdk.log;
using dnproto.sdk.repo;
using dnproto.sdk.ws;
using dnproto.sdk.fs;


namespace dnproto.cli.commands
{
    /// <summary>
    /// A minimal ASP.NET Core web API that responds to /hello with "world"
    /// </summary>
    public class Pds_Run : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"dataDir"});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            var pds = Pds.InitializePds(dataDir, Logger);
            if (pds == null)
            {
                Logger.LogError("Failed to initialize PDS.");
                return;
            }
            pds.Run();
        }
    }
}
