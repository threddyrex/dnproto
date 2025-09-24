using System.Text;

using dnproto.repo;

namespace dnproto.cli.commands
{
    public class PrintRepoStats : BaseCommand
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
            // For stats
            //
            int totalRecords = 0;
            int totalPosts = 0;
            int totalLikes = 0;
            int totalReposts = 0;
            int totalFlashes = 0;
            DateTime earliestDate = DateTime.MaxValue;
            DateTime latestDate = DateTime.MinValue;
            Dictionary<string, int> postsByMonth = new Dictionary<string, int>();
            Dictionary<string, int> repostsByMonth = new Dictionary<string, int>();
            Dictionary<string, int> likesByMonth = new Dictionary<string, int>();
            Dictionary<string, int> recordsByMonth = new Dictionary<string, int>();
            Dictionary<string, int> flashesByMonth = new Dictionary<string, int>();
            Dictionary<string, int> dagCborTypeCounts = new Dictionary<string, int>();
            Dictionary<string, int> recordTypeCounts = new Dictionary<string, int>();

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
                    else if (repoRecord.RecordType == "app.bsky.feed.repost")
                    {
                        totalReposts++;
                    }
                    else if (repoRecord.RecordType == "blue.flashes.feed.post")
                    {
                        totalFlashes++;
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
                        if (recordsByMonth.ContainsKey(month) == false)
                        {
                            recordsByMonth[month] = 0;
                        }
                        recordsByMonth[month]++;

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
                        else if (repoRecord.RecordType == "app.bsky.feed.repost")
                        {
                            if (repostsByMonth.ContainsKey(month) == false)
                            {
                                repostsByMonth[month] = 0;
                            }
                            repostsByMonth[month]++;
                        }
                        else if (repoRecord.RecordType == "blue.flashes.feed.post")
                        {
                            if (flashesByMonth.ContainsKey(month) == false)
                            {
                                flashesByMonth[month] = 0;
                            }
                            flashesByMonth[month]++;
                        }
                    }

                    string typeString = repoRecord.DataBlock.Type.GetMajorTypeString();
                    if (dagCborTypeCounts.ContainsKey(typeString))
                    {
                        dagCborTypeCounts[typeString]++;
                    }
                    else
                    {
                        dagCborTypeCounts[typeString] = 1;
                    }

                    string recordType = repoRecord.RecordType ?? "<null>";

                    if (recordTypeCounts.ContainsKey(recordType))
                    {
                        recordTypeCounts[recordType] = recordTypeCounts[recordType] + 1;
                    }
                    else
                    {
                        recordTypeCounts[recordType] = 1;
                    }


                    return true;
                }
            );

            //
            // Print stats
            //
            Logger.LogInfo("");
            Logger.LogInfo($"repoFile: {repoFile}");
            Logger.LogInfo("");
            Logger.LogInfo($"records: {totalRecords}");
            Logger.LogInfo($"posts: {totalPosts}");
            Logger.LogInfo($"likes: {totalLikes}");
            Logger.LogInfo($"reposts: {totalReposts}");
            Logger.LogInfo($"flashes: {totalFlashes}");
            Logger.LogInfo($"earliestDate: {earliestDate}");
            Logger.LogInfo($"latestDate: {latestDate}");
            Logger.LogInfo("");
            DateTime currentDate = earliestDate;

            while (currentDate <= latestDate.AddMonths(1))
            {
                string month = currentDate.ToString("yyyy-MM");
                int postCount = postsByMonth.ContainsKey(month) ? postsByMonth[month] : 0;
                int likeCount = likesByMonth.ContainsKey(month) ? likesByMonth[month] : 0;
                int recordCount = recordsByMonth.ContainsKey(month) ? recordsByMonth[month] : 0;
                int repostCount = repostsByMonth.ContainsKey(month) ? repostsByMonth[month] : 0;
                int flashCount = flashesByMonth.ContainsKey(month) ? flashesByMonth[month] : 0;
                Logger.LogInfo($"{month}: records={recordCount}, posts={postCount}, likes={likeCount}, reposts={repostCount}, flashes={flashCount}");
                currentDate = currentDate.AddMonths(1);
            }

            Logger.LogInfo("");
            Logger.LogInfo("");
            Logger.LogInfo($"DAG CBOR TYPE COUNTS:");
            Logger.LogInfo("");
            foreach (var kvp in dagCborTypeCounts)
            {
                Logger.LogInfo($"{kvp.Key}: {kvp.Value}");
            }

            Logger.LogInfo("");
            Logger.LogInfo("");
            Logger.LogInfo($"RECORD TYPE COUNTS:");
            Logger.LogInfo("");
            // print in order of most common to least common
            foreach (var kvp in recordTypeCounts.OrderByDescending(kvp => kvp.Value))
            {
                Logger.LogInfo($"{kvp.Key}: {kvp.Value}");
            }
            Logger.LogInfo("");
        }
   }
}