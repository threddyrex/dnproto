using System.Text;

using dnproto.fs;
using dnproto.repo;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands
{
    public class PrintRepoMstRecords : BaseCommand
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
            int totalRecordCount = 0;
            int mstNodeCount = 0;
            int atProtoRecordCount = 0;
            int mstNodeEmptyCount = 0;
            int commitCount = 0;
            int nodeSubtreeCount = 0;
            int entrySubtreeCount = 0;

            Dictionary<string, MstNode> mstNodes = new Dictionary<string, MstNode>();


            //
            // Walk repo
            //
            Repo.WalkRepo(
                repoFile,
                (repoHeader) =>
                {
                    return true;
                },
                (repoRecord) =>
                {
                    totalRecordCount++;
                    MstNode? mstNode = repoRecord.ToMstNode();
                    if(mstNode != null)
                    {
                        mstNodeCount++;
                        mstNodes[repoRecord.Cid.ToString()] = mstNode;
                        Logger.LogTrace($"MST Node CID: {repoRecord.Cid}");
                        foreach(var entry in mstNode.Entries)
                        {
                            Logger.LogTrace("       (e) Entry");
                            Logger.LogTrace($"              (k) KeySuffix: {entry.KeySuffix}");
                            Logger.LogTrace($"              (p) PrefixLength: {entry.PrefixLength}");
                            Logger.LogTrace($"              (t) TreeMstNodeCid: {entry.TreeMstNodeCid}");
                            Logger.LogTrace($"              (v) RecordCid: {entry.RecordCid}");
                            Logger.LogTrace("");

                            if(entry.TreeMstNodeCid != null)
                            {
                                entrySubtreeCount++;
                            }
                        }

                        if(mstNode.Entries.Count == 0)
                        {
                            mstNodeEmptyCount++;
                        }

                        if(mstNode.LeftMstNodeCid != null)
                        {
                            nodeSubtreeCount++;
                        }
                    }

                    if(repoRecord.IsAtProtoRecord())
                    {
                        atProtoRecordCount++;
                    }

                    RepoCommit? repoCommit = repoRecord.ToRepoCommit();
                    if(repoCommit != null)
                    {
                        commitCount++;
                    }

                    return true;
                }
            );


            //
            // Print stats
            //
            Logger.LogInfo("");
            Logger.LogInfo("");
            Logger.LogInfo($"totalRecordCount: {totalRecordCount}");
            Logger.LogInfo("");
            Logger.LogInfo($"totalRecordCount sum: {mstNodeCount + commitCount + atProtoRecordCount}");
            Logger.LogInfo($"   mstNodeCount: {mstNodeCount}");
            Logger.LogInfo($"   commitCount: {commitCount}");
            Logger.LogInfo($"   atProtoRecordCount: {atProtoRecordCount}");
            Logger.LogInfo("");
            Logger.LogInfo($"mstNodeEmptyCount: {mstNodeEmptyCount}");
            Logger.LogInfo("");
            Logger.LogInfo($"totalSubtreeCount: {nodeSubtreeCount + entrySubtreeCount}");
            Logger.LogInfo($"   nodeSubtreeCount: {nodeSubtreeCount}");
            Logger.LogInfo($"   entrySubtreeCount: {entrySubtreeCount}");
            Logger.LogInfo("");
            Logger.LogInfo("");


        }
   }
}