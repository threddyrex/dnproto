using System.Text;

using dnproto.sdk.repo;
using dnproto.sdk.ws;
using dnproto.sdk.fs;

namespace dnproto.cli.commands
{
    public class PrintRepoPosts : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"actor"});
        }

        public override HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>(new string[]{"month"});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");
            string? month = CommandLineInterface.GetArgumentValue(arguments, "month");

            //
            // Load lfs
            //
            LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
            ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

            //
            // Get local path of repo file (assumes user called GetRepo first to pull it down).
            //
            string? repoFile = lfs?.GetPath_RepoFile(actorInfo);
            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError($"Repo file does not exist: {repoFile}");
                return;
            }

            //
            // Walk repo once to get cid/rkey mapping
            //
            Dictionary<string, string>? rkeys = RepoUtils.FindRkeys(repoFile);

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


                    if (string.IsNullOrEmpty(month) == false)
                    {
                        if (DateTime.TryParse(repoRecord.CreatedAt, out DateTime createdAt))
                        {
                            string postMonth = createdAt.ToString("yyyy-MM");
                            if (month.Equals(postMonth))
                            {
                                posts.Add(repoRecord);
                            }
                        }
                    }
                    else
                    {
                        posts.Add(repoRecord);
                    }

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
                    Logger.LogInfo($"[{repoRecord.DataBlock.SelectString(["createdAt"])}] https://bsky.app/profile/{actorInfo?.Did}/post/{rkey}");
                }
                else
                {
                    Logger.LogInfo($"[{repoRecord.DataBlock.SelectString(["createdAt"])}] {repoRecord.Cid?.GetBase32()}");
                }

                //
                // Print text content
                //
                string? text = repoRecord.JsonString;
                Logger.LogTrace(text);
            }
        }
   }
}