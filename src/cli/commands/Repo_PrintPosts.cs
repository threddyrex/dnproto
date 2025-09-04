using System.Text;

using dnproto.repo;

namespace dnproto.cli.commands
{
    public class Repo_PrintPosts : BaseCommand
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

            if (string.IsNullOrEmpty(repoFile))
            {
                Logger.LogError("repoFile is empty.");
                return;
            }

            bool fileExists = File.Exists(repoFile);

            if (!fileExists)
            {
                Logger.LogError("File does not exist.");
                return;
            }

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
                    if (string.IsNullOrEmpty(repoRecord.RecordType)) return true;
                    if (string.Equals(repoRecord.RecordType, "app.bsky.feed.post", StringComparison.OrdinalIgnoreCase) == false) return true;

                    Console.WriteLine($"cid: {repoRecord.Cid.GetBase32()}");
                    Console.WriteLine($"createdAt: {repoRecord.DataBlock.SelectString(["createdAt"])}");
                    Console.WriteLine($"text: {repoRecord.DataBlock.SelectString(["text"])}");
                    return true;
                }
            );
        }
   }
}