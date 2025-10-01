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
            // Get local path of repo file (assumes user called GetRepo first to pull it down).
            //
            string? repoFile = LocalFileSystem.Initialize(dataDir, Logger)?.GetPath_RepoFile(handle);
            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError($"Repo file does not exist: {repoFile}");
                return;
            }

            //
            // Walk repo once to get cid/rkey mapping
            //
            Dictionary<string, string>? rkeys = Repo.FindRkeys(repoFile);

            //
            // Walk repo
            //
            List<RepoRecord> posts = new List<RepoRecord>();

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

                    posts.Add(repoRecord);
                    return true;
                }
            );

            //
            // Print, sorted
            //
            var sortedPosts = posts.OrderBy(pr => pr.DataBlock.SelectString(["createdAt"]));
            foreach (var repoRecord in sortedPosts)
            {
                string? rkey = rkeys != null && repoRecord.Cid != null && rkeys.TryGetValue(repoRecord.Cid.GetBase32(), out string? foundRkey) ? foundRkey : null;

                if (string.IsNullOrEmpty(rkey) == false)
                {
                    Console.WriteLine($"[{repoRecord.DataBlock.SelectString(["createdAt"])}] https://bsky.app/profile/{handle}/post/{rkey}");
                }
                else
                {
                    Console.WriteLine($"[{repoRecord.DataBlock.SelectString(["createdAt"])}] {repoRecord.Cid?.GetBase32()}");
                }
            }
        }
   }
}