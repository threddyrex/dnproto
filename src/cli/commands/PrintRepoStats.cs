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

            Dictionary<string, int> recordTypeCounts = new Dictionary<string, int>();
            Dictionary<string, List<RepoRecord>> recordsByMonth = new Dictionary<string, List<RepoRecord>>();


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
                            recordsByMonth[month] = new List<RepoRecord>();
                        }

                        recordsByMonth[month].Add(repoRecord);
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
            Logger.LogInfo($"earliestDate: {earliestDate}");
            Logger.LogInfo($"latestDate: {latestDate}");
            Logger.LogInfo("");
            DateTime currentDate = earliestDate;

            while (currentDate <= latestDate.AddMonths(1))
            {
                string month = currentDate.ToString("yyyy-MM");

                int recordCount = recordsByMonth.ContainsKey(month) ? recordsByMonth[month].Count() : 0;
                int postCount = recordsByMonth.ContainsKey(month) ? recordsByMonth[month].Where(r => r.RecordType == RecordType.BLUESKY_POST).Count() : 0;
                int likeCount = recordsByMonth.ContainsKey(month) ? recordsByMonth[month].Where(r => r.RecordType == RecordType.BLUESKY_LIKE).Count() : 0;
                int repostCount = recordsByMonth.ContainsKey(month) ? recordsByMonth[month].Where(r => r.RecordType == RecordType.BLUESKY_REPOST).Count() : 0;
                int followCount = recordsByMonth.ContainsKey(month) ? recordsByMonth[month].Where(r => r.RecordType == RecordType.BLUESKY_FOLLOW).Count() : 0;
                int blockCount = recordsByMonth.ContainsKey(month) ? recordsByMonth[month].Where(r => r.RecordType == RecordType.BLUESKY_BLOCK).Count() : 0;
                int flashCount = recordsByMonth.ContainsKey(month) ? recordsByMonth[month].Where(r => r.RecordType == RecordType.FLASHES_POST).Count() : 0;

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