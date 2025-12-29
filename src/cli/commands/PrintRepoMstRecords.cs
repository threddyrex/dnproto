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
            int mstNodeEmptyCount = 0;


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

                    if(MstNode.IsMstNode(repoRecord.DataBlock))
                    {
                        mstNodeCount++;
                    }
                    else
                    {
                        return true;
                    }

                    MstNode? mstNode = MstNode.FromDagCborObject(repoRecord.DataBlock);
                    if(mstNode != null)
                    {
                        Logger.LogTrace($"MST Node CID: {repoRecord.Cid}");
                        foreach(var entry in mstNode.Entries)
                        {
                            Logger.LogTrace($"  Entry - PrefixLength: {entry.PrefixLength}, KeySuffix: {entry.KeySuffix}, RecordCid: {entry.RecordCid}, TreeMstNodeCid: {entry.TreeMstNodeCid}");
                        }

                        if(mstNode.Entries.Count == 0)
                        {
                            mstNodeEmptyCount++;
                            Logger.LogTrace(repoRecord.JsonString);
                        }
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
            Logger.LogInfo($"mstNodeCount: {mstNodeCount}");
            Logger.LogInfo($"mstNodeEmptyCount: {mstNodeEmptyCount}");
            Logger.LogInfo("");
            Logger.LogInfo("");

        }
   }
}