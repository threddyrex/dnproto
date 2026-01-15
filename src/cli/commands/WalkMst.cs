using System.Text;

using dnproto.fs;
using dnproto.mst;
using dnproto.repo;
using dnproto.ws;
using dnproto.uri;
using System.Security.Cryptography.X509Certificates;

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
            // Load mst
            //
            List<MstItem> mstItems = RepoMst.LoadMstItemsFromRepo(repoFile, Logger);
            Mst mst = Mst.AssembleTreeFromItems(mstItems);
            List<MstNode> allMstNodes = mst.FindAllNodes();
            Dictionary<MstNode, (CidV1, DagCborObject)> mstNodeCache = new Dictionary<MstNode, (CidV1, DagCborObject)>();
            foreach (MstNode node in allMstNodes)
            {
                RepoMst.ConvertMstNodeToDagCbor(mstNodeCache, node);
            }

            //
            // Stats
            //
            int mstEntryCount = 0;
            foreach(var node in allMstNodes)
            {
                mstEntryCount += node.Entries.Count;
            }
            Logger.LogInfo("");
            Logger.LogInfo($"mstItems.Count: {mstItems.Count}");
            Logger.LogInfo($"allMstNodes.Count: {allMstNodes.Count}");
            Logger.LogInfo($"mstNodeCache.Keys.Count: {mstNodeCache.Keys.Count}");
            Logger.LogInfo($"mstEntryCount: {mstEntryCount}");
            Logger.LogInfo("");


            //
            // Walk
            //
            Logger.LogTrace("");
            VisitNode(mstNodeCache, mst.Root, 0, "root");
            Logger.LogTrace("");


        }

        public void VisitNode(Dictionary<MstNode, (CidV1, DagCborObject)> mstNodeCache, MstNode node, int indent, string direction)
        {
            Logger.LogTrace($"{new string(' ', indent)} [{direction}] [{node.KeyDepth}] {mstNodeCache[node].Item1}");

            foreach(var entry in node.Entries)
            {
                Logger.LogTrace($"{new string(' ', indent)} {entry.Key}: {entry.Value}");
            }

            Logger.LogTrace("");

            if(node.LeftTree != null)
            {
                VisitNode(mstNodeCache, node.LeftTree, indent + 2, "left");
                Logger.LogTrace("");
            }


            foreach(var entry in node.Entries)
            {
                if(entry.RightTree != null)
                {
                    VisitNode(mstNodeCache, entry.RightTree, indent + 2, "right");
                }
            }
        }

   }
}