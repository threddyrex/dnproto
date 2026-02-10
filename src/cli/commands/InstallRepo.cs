

using dnproto.fs;
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
            string? dataDir = LocalFileSystem?.GetDataDir();

            if(dataDir is null)
            {
                throw new Exception("DataDir is null");
            }

            //
            // Load PDS
            //
            var pds = Pds.InitializePdsForRun(dataDir, Logger);
            var pdsDb = pds.PdsDb;
            var func = dnproto.auth.Signer.CreateCommitSigningFunction(pdsDb.GetConfigProperty("UserPrivateKeyMultibase"), pdsDb.GetConfigProperty("UserPublicKeyMultibase"));

            if(pdsDb.GetConfigProperty("UserDid") == null)
            {
                Logger.LogError("Cannot install MST: User DID is null.");
                return;
            }


            //
            // Install repo (destroy existing one if any)
            //
            if(LocalFileSystem is null)
            {
                throw new Exception("LocalFileSystem is null");
            }
            
            Installer.InstallRepo(LocalFileSystem, Logger, func);
        }
    }
}
