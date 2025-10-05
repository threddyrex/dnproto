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
            // Get local repo file
            //
            string? repoFile = LocalFileSystem.Initialize(dataDir, Logger)?.GetPath_RepoFile(handle);
            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError($"Repo file does not exist: {repoFile}");
                return;
            }


            //
            // For stats
            //
            DateTime earliestDate = DateTime.MaxValue;
            DateTime latestDate = DateTime.MinValue;

            // <type, count>
            Dictionary<string, int> recordTypeCounts = new Dictionary<string, int>();
            // <month, count>
            Dictionary<string, int> recordCountsByMonth = new Dictionary<string, int>();
            // <month, <type, count>>
            Dictionary<string, Dictionary<string, int>> recordTypeCountsByMonth = new Dictionary<string, Dictionary<string, int>>();



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
                    string recordType = repoRecord.RecordType ?? "<null>";

                    // total counts
                    if (recordTypeCounts.ContainsKey(recordType))
                    {
                        recordTypeCounts[recordType] = recordTypeCounts[recordType] + 1;
                    }
                    else
                    {
                        recordTypeCounts[recordType] = 1;
                    }


                    // counts by month
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

                        if (recordTypeCountsByMonth.ContainsKey(month) == false)
                        {
                            recordTypeCountsByMonth[month] = new Dictionary<string, int>();
                        }

                        if (recordCountsByMonth.ContainsKey(month) == false)
                        {
                            recordCountsByMonth[month] = 0;
                        }

                        if (recordTypeCountsByMonth[month].ContainsKey(recordType) == false)
                        {
                            recordTypeCountsByMonth[month][recordType] = 0;
                        }

                        recordCountsByMonth[month] = recordCountsByMonth[month] + 1;
                        recordTypeCountsByMonth[month][recordType] = recordTypeCountsByMonth[month][recordType] + 1;
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
            Logger.LogInfo($"earliestDate: {earliestDate}");
            Logger.LogInfo($"latestDate: {latestDate}");
            Logger.LogInfo("");
            DateTime currentDate = earliestDate;

            while (currentDate <= latestDate.AddMonths(1))
            {
                string month = currentDate.ToString("yyyy-MM");

                int recordCount = recordCountsByMonth.ContainsKey(month) ? recordCountsByMonth[month] : 0;
                int postCount = recordTypeCountsByMonth.ContainsKey(month) && recordTypeCountsByMonth[month].ContainsKey(RecordType.BLUESKY_POST) ? recordTypeCountsByMonth[month][RecordType.BLUESKY_POST] : 0;
                int likeCount = recordTypeCountsByMonth.ContainsKey(month) && recordTypeCountsByMonth[month].ContainsKey(RecordType.BLUESKY_LIKE) ? recordTypeCountsByMonth[month][RecordType.BLUESKY_LIKE] : 0;
                int repostCount = recordTypeCountsByMonth.ContainsKey(month) && recordTypeCountsByMonth[month].ContainsKey(RecordType.BLUESKY_REPOST) ? recordTypeCountsByMonth[month][RecordType.BLUESKY_REPOST] : 0;
                int followCount = recordTypeCountsByMonth.ContainsKey(month) && recordTypeCountsByMonth[month].ContainsKey(RecordType.BLUESKY_FOLLOW) ? recordTypeCountsByMonth[month][RecordType.BLUESKY_FOLLOW] : 0;
                int blockCount = recordTypeCountsByMonth.ContainsKey(month) && recordTypeCountsByMonth[month].ContainsKey(RecordType.BLUESKY_BLOCK) ? recordTypeCountsByMonth[month][RecordType.BLUESKY_BLOCK] : 0;
                int flashCount = recordTypeCountsByMonth.ContainsKey(month) && recordTypeCountsByMonth[month].ContainsKey(RecordType.FLASHES_POST) ? recordTypeCountsByMonth[month][RecordType.FLASHES_POST] : 0;

                Logger.LogInfo($"{month}: records={recordCount}, follows={followCount}, posts={postCount}, likes={likeCount}, reposts={repostCount}, blocks={blockCount}, flashes={flashCount}");
                currentDate = currentDate.AddMonths(1);
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