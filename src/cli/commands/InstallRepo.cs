

using dnproto.pds;


namespace dnproto.cli.commands
{
    /// <summary>
    /// Installs a new repo for the user.
    /// </summary>
    public class InstallRepo : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get Arguments
            //
            string? dataDir = LocalFileSystem?.DataDir;

            //
            // Load PDS
            //
            var pds = Pds.InitializePdsForRun(dataDir!, Logger);
            var pdsDb = pds?.PdsDb;
            var func = pds?.CommitSigningFunction;

            if(pdsDb == null || func == null)
            {
                Logger.LogError("Cannot install MST: PDS DB or commit signing function is null.");
                return;
            }

            if(pds?.Config.UserDid == null)
            {
                Logger.LogError("Cannot install MST: User DID is null.");
                return;
            }


            //
            // Install repo (destroy existing one if any)
            //
            Installer.InstallRepo(dataDir, Logger, func);
        }
    }
}
