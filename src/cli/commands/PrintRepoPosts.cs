using System.Text;

using dnproto.repo;

namespace dnproto.cli.commands
{
    public class PrintRepoPosts : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"dataDir", "handle"});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");

            //
            // Get local file system
            //
            LocalFileSystem? localFileSystem = LocalFileSystem.Initialize(dataDir, Logger);
            if (localFileSystem == null)
            {
                Logger.LogError("Failed to initialize local file system.");
                return;
            }

    
            string? repoFile = localFileSystem.GetPath_RepoFile(handle);
            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError($"Repo file does not exist: {repoFile}");
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