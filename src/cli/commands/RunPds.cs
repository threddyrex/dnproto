

using dnproto.pds;


namespace dnproto.cli.commands
{
    /// <summary>
    /// A minimal ASP.NET Core web API that responds to /hello with "world"
    /// </summary>
    public class RunPds : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            var pds = Pds.InitializePdsForRun(dataDir, Logger);
            if (pds == null)
            {
                Logger.LogError("Failed to initialize PDS.");
                return;
            }
            pds.Run();
        }
    }
}
