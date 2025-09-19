using System.Text;

using dnproto.repo;

namespace dnproto.cli.commands
{
    public class PrintRepoCounts : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"repofile"});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? repoFile = CommandLineInterface.GetArgumentValue(arguments, "repofile");

            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError("repofile is not valid.");
                return;
            }


            //
            // For stats
            //
            int totalRecords = 0;
            int totalPosts = 0;
            int totalLikes = 0;
            DateTime earliestDate = DateTime.MaxValue;
            DateTime latestDate = DateTime.MinValue;
            Dictionary<string, int> postsByMonth = new Dictionary<string, int>();
            Dictionary<string, int> likesByMonth = new Dictionary<string, int>();

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
                    // basic stats
                    totalRecords++;
                    if (repoRecord.RecordType == "app.bsky.feed.post")
                    {
                        totalPosts++;
                    }
                    else if (repoRecord.RecordType == "app.bsky.feed.like")
                    {
                        totalLikes++;
                    }

                    if (DateTime.TryParse(repoRecord.CreatedAt, out DateTime createdAt))
                    {
                        if (createdAt < earliestDate)
                        {
                            earliestDate = createdAt;
                        }
                        if (createdAt > latestDate)
                        {
                            latestDate = createdAt;
                        }

                        string month = createdAt.ToString("yyyy-MM");
                        if (repoRecord.RecordType == "app.bsky.feed.post")
                        {
                            if (postsByMonth.ContainsKey(month) == false)
                            {
                                postsByMonth[month] = 0;
                            }
                            postsByMonth[month]++;
                        }
                        else if (repoRecord.RecordType == "app.bsky.feed.like")
                        {
                            if (likesByMonth.ContainsKey(month) == false)
                            {
                                likesByMonth[month] = 0;
                            }
                            likesByMonth[month]++;
                        }
                    }

                    return true;
                }
            );

            //
            // Print stats
            //
            Logger.LogInfo($"records: {totalRecords}");
            Logger.LogInfo($"posts: {totalPosts}");
            Logger.LogInfo($"likes: {totalLikes}");
            Logger.LogInfo($"earliestDate: {earliestDate}");
            Logger.LogInfo($"latestDate: {latestDate}");

            DateTime currentDate = earliestDate;

            while (currentDate <= latestDate.AddMonths(1))
            {
                string month = currentDate.ToString("yyyy-MM");
                int postCount = postsByMonth.ContainsKey(month) ? postsByMonth[month] : 0;
                int likeCount = likesByMonth.ContainsKey(month) ? likesByMonth[month] : 0;
                Logger.LogTrace($"{month}: posts={postCount}, likes={likeCount}");
                currentDate = currentDate.AddMonths(1);
            }
        }
   }
}