

using dnproto.pds;


namespace dnproto.cli.commands
{
    /// <summary>
    /// Install config
    /// </summary>
    public class InstallConfig : BaseCommand
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
            string? pdsHostname = CommandLineInterface.GetArgumentValue(arguments, "pdshostname");
            string? availableUserDomain = CommandLineInterface.GetArgumentValue(arguments, "availableuserdomain");
            string? userHandle = CommandLineInterface.GetArgumentValue(arguments, "userHandle");
            string? userDid = CommandLineInterface.GetArgumentValue(arguments, "userDid");
            string? userEmail = CommandLineInterface.GetArgumentValue(arguments, "userEmail");

            //
            // Install config
            //
            if(LocalFileSystem is null)
            {
                throw new Exception("LocalFileSystem is null");
            }
            Installer.InstallConfig(LocalFileSystem, Logger, pdsHostname, availableUserDomain, userHandle, userDid, userEmail);
        }
    }
}
