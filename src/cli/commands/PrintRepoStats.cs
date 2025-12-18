using System.Text;

using dnproto.sdk.repo;
using dnproto.sdk.ws;
using dnproto.sdk.fs;

namespace dnproto.cli.commands
{
    public class PrintRepoStats : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"dataDir", "actor"});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");

            //
            // Load lfs
            //
            LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
            ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

            //
            // Get local repo file
            //
            string? repoFile = LocalFileSystem.Initialize(dataDir, Logger)?.GetPath_RepoFile(actorInfo);
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
            Dictionary<string, int> recordTypeCounts = new Dictionary<string, int>(); // <type, count>
            Dictionary<string, int> recordCountsByMonth = new Dictionary<string, int>(); // <month, count>
            Dictionary<string, Dictionary<string, int>> recordTypeCountsByMonth = new Dictionary<string, Dictionary<string, int>>(); // <month, <type, count>>
            int errorCount = 0;
            int totalRecordCount = 0;


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
                    totalRecordCount++;

                    if (repoRecord.IsError)
                    {
                        errorCount++;
                        Logger.LogTrace($"ERROR: {repoRecord.JsonString}");
                        return true;
                    }

                    string recordType = repoRecord.RecordType ?? "<null>";

                    //
                    // total counts
                    //
                    if (recordTypeCounts.ContainsKey(recordType) == false)
                    {
                        recordTypeCounts[recordType] = 0;
                    }

                    recordTypeCounts[recordType] = recordTypeCounts[recordType] + 1;


                    //
                    // counts by month
                    //
                    if (DateTime.TryParse(repoRecord.CreatedAt, out DateTime createdAt))
                    {
                        earliestDate = createdAt < earliestDate ? createdAt : earliestDate;
                        latestDate = createdAt > latestDate ? createdAt : latestDate;

                        string month = createdAt.ToString("yyyy-MM");

                        //
                        // Initialize dictionaries if needed
                        //
                        if (recordTypeCountsByMonth.ContainsKey(month) == false)
                        {
                            recordTypeCountsByMonth[month] = new Dictionary<string, int>();
                            recordCountsByMonth[month] = 0;
                        }

                        if (recordTypeCountsByMonth[month].ContainsKey(recordType) == false)
                        {
                            recordTypeCountsByMonth[month][recordType] = 0;
                        }

                        //
                        // Increment counts
                        //
                        recordCountsByMonth[month] = recordCountsByMonth[month] + 1;
                        recordTypeCountsByMonth[month][recordType] = recordTypeCountsByMonth[month][recordType] + 1;
                    }



                    return true;
                }
            );

            if (earliestDate == DateTime.MaxValue || latestDate == DateTime.MinValue)
            {
                Logger.LogError($"No valid record dates found in repo: {repoFile}");
                return;
            }

            //
            // Print stats
            //
            Logger.LogInfo("");
            Logger.LogInfo($"repoFile: {repoFile}");
            Logger.LogInfo("");
            Logger.LogInfo($"earliestDate: {earliestDate}");
            Logger.LogInfo($"latestDate: {latestDate}");
            Logger.LogInfo("");
            Logger.LogInfo($"errorCount: {errorCount}");
            Logger.LogInfo("");
            Logger.LogInfo($"totalRecordCount: {totalRecordCount}");
            Logger.LogInfo("");
            DateTime currentDate = earliestDate;

            while (currentDate <= latestDate.AddMonths(1))
            {
                string currentDateMonth = currentDate.ToString("yyyy-MM");

                int recordCount = 0;
                int postCount = 0;
                int likeCount = 0;
                int repostCount = 0;
                int followCount = 0;
                int blockCount = 0;
                int flashCount = 0;
                int verificationCount = 0;

                recordCount = recordCountsByMonth.TryGetValue(currentDateMonth, out int rc) ? rc : 0;

                if (recordTypeCountsByMonth.TryGetValue(currentDateMonth, out var typeCounts))
                {
                    postCount = typeCounts.TryGetValue(RecordType.BLUESKY_POST, out int pc) ? pc : 0;
                    likeCount = typeCounts.TryGetValue(RecordType.BLUESKY_LIKE, out int lc) ? lc : 0;
                    repostCount = typeCounts.TryGetValue(RecordType.BLUESKY_REPOST, out int rrc) ? rrc : 0;
                    followCount = typeCounts.TryGetValue(RecordType.BLUESKY_FOLLOW, out int fc) ? fc : 0;
                    blockCount = typeCounts.TryGetValue(RecordType.BLUESKY_BLOCK, out int bc) ? bc : 0;
                    flashCount = typeCounts.TryGetValue(RecordType.FLASHES_POST, out int fsc) ? fsc : 0;
                    verificationCount = typeCounts.TryGetValue(RecordType.VERIFICATION, out int vc) ? vc : 0;
                }

                Logger.LogInfo($"{currentDateMonth}: records={recordCount}, follows={followCount}, posts={postCount}, likes={likeCount}, reposts={repostCount}, blocks={blockCount}, flashes={flashCount}, verifications={verificationCount}");
                currentDate = currentDate.AddMonths(1);
            }



            //
            // Print record type counts, descending
            //
            Logger.LogInfo("");
            Logger.LogInfo("");
            Logger.LogInfo($"RECORD TYPE COUNTS:");
            Logger.LogInfo("");
            foreach (var kvp in recordTypeCounts.OrderByDescending(kvp => kvp.Value))
            {
                Logger.LogInfo($"{kvp.Key}: {kvp.Value}");
            }
            Logger.LogInfo("");
            
            if(errorCount > 0)
            {
                Logger.LogInfo($"Note: {errorCount} records could not be parsed. Use log level 'trace' to see details.");
                Logger.LogInfo("");
            }
        }
   }
}