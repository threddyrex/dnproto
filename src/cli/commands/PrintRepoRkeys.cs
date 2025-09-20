using System.Text;

using dnproto.repo;

namespace dnproto.cli.commands
{
    public class PrintRepoRkeys : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return [.. new string[]{"repoFile"}];
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? repoFile = CommandLineInterface.GetArgumentValue(arguments, "repoFile");

            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError("repoFile is empty or does not exist.");
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