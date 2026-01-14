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
            Mst mst = RepoMst.LoadMstFromRepo(repoFile);


            //
            // Walk
            //
            VisitNode(mst.Root, 0, "root");


        }

        public void VisitNode(MstNode node, int indent, string direction)
        {
            Logger.LogInfo($"{new string(' ', indent)} [{direction}] [{node.KeyDepth}] ");

            foreach(var entry in node.Entries)
            {
                Logger.LogInfo($"{new string(' ', indent)} {entry.Key}: {entry.Value}");
            }

            Logger.LogInfo("");

            if(node.LeftTree != null)
            {
                VisitNode(node.LeftTree, indent + 2, "left");                
                Logger.LogInfo("");
            }


            foreach(var entry in node.Entries)
            {
                if(entry.RightTree != null)
                {
                    VisitNode(entry.RightTree, indent + 2, "right");
                }
            }
        }

   }
}