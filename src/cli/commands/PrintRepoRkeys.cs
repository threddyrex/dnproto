using System.Text;

using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands
{
    public class PrintRepoRkeys : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"dataDir", "actor"});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");

            // resolve handle
            var handleInfo = BlueskyClient.ResolveHandleInfo(actor);

            //
            // Get local file system
            //
            LocalFileSystem? localFileSystem = LocalFileSystem.Initialize(dataDir, Logger);
            if (localFileSystem == null)
            {
                Logger.LogError("Failed to initialize local file system.");
                return;
            }

    
            string? repoFile = localFileSystem.GetPath_RepoFile(handleInfo);
            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError($"Repo file does not exist: {repoFile}");
                return;
            }



            //
            // Print rkeys
            //
            Dictionary<string, string>? rkeys = Repo.FindRkeys(repoFile);

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