

using dnproto.pds;
using dnproto.pds.db;


namespace dnproto.cli.commands
{
    /// <summary>
    /// A minimal ASP.NET Core web API that responds to /hello with "world"
    /// </summary>
    public class InstallMst : BaseCommand
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
            // Initialize MST (destructive)
            //
            var pds = Pds.LoadPdsForRun(dataDir!, Logger);
            var pdsDb = pds?.PdsDb;
            var func = pds?.CommitSigningFunction;

            if(pdsDb == null || func == null)
            {
                Logger.LogError("Cannot install MST: PDS DB or commit signing function is null.");
                return;
            }

            var mst = new Mst(pdsDb, Logger, func, pds?.Config.UserDid);

            mst.InstallMst();
        }
    }
}
