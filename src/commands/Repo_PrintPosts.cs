using System.Text;

using dnproto.repo;
using dnproto.utils;

namespace dnproto.commands
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
                Console.WriteLine("repoFile is empty.");
                return;
            }

            bool fileExists = File.Exists(repoFile);

            if (!fileExists)
            {
                Console.WriteLine("File does not exist.");
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

                    Console.WriteLine($"-----------------------------------------------------------------------------------------------------------");
                    Console.WriteLine($"cid:");
                    Console.WriteLine($"{repoRecord.Cid.GetBase32()}");
                    Console.WriteLine();
                    Console.WriteLine($"createdAt:");
                    Console.WriteLine(repoRecord.DataBlock.SelectString(["createdAt"]));
                    Console.WriteLine();
                    Console.WriteLine($"text:");
                    Console.WriteLine(repoRecord.DataBlock.SelectString(["text"]));
                    Console.WriteLine();
                    return true;
                }
            );
        }
   }
}