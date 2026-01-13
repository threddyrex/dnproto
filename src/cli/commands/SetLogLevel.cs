

using dnproto.pds;


namespace dnproto.cli.commands
{

    /// <summary>
    /// </summary>
    public class SetLogLevel : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"newlevel"});
        }


        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get Arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? newLevel = CommandLineInterface.GetArgumentValue(arguments, "newlevel");

            if(LocalFileSystem is null
                || Logger is null
                || string.IsNullOrEmpty(newLevel))
            {
                throw new Exception("LocalFileSystem or Logger is null");
            }

            PdsDb pdsDb = PdsDb.ConnectPdsDb(LocalFileSystem, Logger);
            Logger.LogInfo($"Setting log level to {newLevel}");
            pdsDb.SetLogLevel(newLevel);
            Logger.LogInfo($"New log level count: {pdsDb.GetLogLevelCount()}");
        }
    }
}
