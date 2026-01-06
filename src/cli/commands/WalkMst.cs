using System.Text;

using dnproto.fs;
using dnproto.repo;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands
{
    public class WalkMst : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{});
        }

        public override HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>(new string[]{"actor", "repofile"});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");
            string? repoFileArg = CommandLineInterface.GetArgumentValue(arguments, "repofile");


            //
            // Find repo file
            //
            string? repoFile = repoFileArg;

            if(string.IsNullOrEmpty(repoFileArg) && string.IsNullOrEmpty(actor) == false)
            {
                ActorInfo? actorInfo = LocalFileSystem?.ResolveActorInfo(actor);
                repoFile = LocalFileSystem?.GetPath_RepoFile(actorInfo);
            }

            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError($"Repo file does not exist: {repoFile}");
                return;
            }


            //
            // For stats
            //
            int nodeCount = 0;
            int errorCount = 0;


            //
            // First walk repo and load everything.
            //
            Mst.WalkMst(repoFile,
                (repoCommit) =>
                {
                    Logger.LogInfo($"RepoCommit.Cid: {repoCommit.Cid}");
                    Logger.LogInfo($"RepoCommit.RootMstNodeCid: {repoCommit.RootMstNodeCid}");
                    return true;
                },
                (mstNode, currentDepth, mstEntries) =>
                {
                    // print node cid
                    Logger.LogInfo($"{new string(' ', currentDepth * 2)}MST Node: {mstNode.Cid}");
                    nodeCount++;
                    return true;
                },
                (errorMsg) =>
                {
                    Logger.LogError($"Error walking MST: {errorMsg}");
                    errorCount++;
                    return true;
                });
            

            Logger.LogInfo($"MST verification complete. Nodes visited: {nodeCount}, Errors found: {errorCount}");
        }

   }
}