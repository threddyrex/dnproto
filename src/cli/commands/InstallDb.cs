

using dnproto.pds;


namespace dnproto.cli.commands
{

    /// <summary>
    /// A minimal ASP.NET Core web API that responds to /hello with "world"
    /// </summary>
    public class InstallDb : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"deleteExistingDb"});
        }


        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get Arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            bool deleteExistingDb = CommandLineInterface.GetArgumentValueWithDefault(arguments, "deleteExistingDb", false);

            //
            // Install db
            //
            Installer.InstallDb(dataDir!, Logger, deleteExistingDb);
        }
    }
}
