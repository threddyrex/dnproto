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
            // Walk MST. It takes care of parsing RepoRecords.
            //
            Mst.WalkMst(repoFile,
                // data loaded callback
                (repoHeader, repoCommit, mstNodes, mstNodeEntries, atProtoRecordCids) =>
                {
                    Logger.LogInfo("Beginning MST Walk...");
                    Logger.LogInfo($"   RepoHeader.Cid: {repoHeader.RepoCommitCid}");
                    Logger.LogInfo($"   RepoCommit.Cid: {repoCommit.Cid}");
                    Logger.LogInfo($"   RepoCommit.RootMstNodeCid: {repoCommit.RootMstNodeCid}");
                    Logger.LogInfo($"   Total MST Nodes Loaded: {mstNodes.Count}");
                    Logger.LogInfo($"   Total AtProto Record CIDs: {atProtoRecordCids.Count}");
                    return true;
                },
                // mst node callback
                (direction, mstNode, currentDepth, mstEntries) =>
                {
                    Logger.LogTrace($"{new string(' ', currentDepth * 2)}({currentDepth}) {direction}{mstNode.Cid}");
                    nodeCount++;

                    var fullKeys = MstEntry.GetFullKeys(mstEntries);
                    foreach(var fullKey in fullKeys)
                    {
                        Logger.LogTrace($"{new string(' ', (currentDepth + 1) * 2)}- {fullKey}");
                    }

                    return true;
                },
                // error callback
                (errorMsg) =>
                {
                    Logger.LogError($"Error walking MST: {errorMsg}");
                    errorCount++;
                    return true;
                });
            

            Logger.LogInfo($"MST walk complete.");
            Logger.LogInfo($"   Nodes visited: {nodeCount}");
            Logger.LogInfo($"   Errors found: {errorCount}");
        }

   }
}