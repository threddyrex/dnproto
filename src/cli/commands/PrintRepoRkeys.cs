using System.Text;

using dnproto.fs;
using dnproto.repo;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands
{
    public class PrintRepoRkeys : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"actor"});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");

            //
            // Load lfs
            //
            LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
            ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

            string? repoFile = lfs?.GetPath_RepoFile(actorInfo);
            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError($"Repo file does not exist: {repoFile}");
                return;
            }



            //
            // Print rkeys
            //
            Dictionary<string, string>? rkeys = RepoUtils.FindRkeys(repoFile);

            if (rkeys == null || rkeys.Count == 0)
            {
                Logger.LogInfo("No rkeys found.");
                return;
            }

            foreach (KeyValuePair<string, string> kvp in rkeys)
            {
                Logger.LogInfo($"{kvp.Key} => {kvp.Value}");
            }
        }
   }
}