using System.Text;

using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands
{
    public class PrintRepoRecords : BaseCommand
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
    
            string? repoFile = lfs?.GetPath_RepoFile(actorInfo);
            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError($"Repo file does not exist: {repoFile}");
                return;
            }


            //
            // For stats
            //
            int totalRecords = 0;
            Dictionary<string, int> dagCborTypeCounts = new Dictionary<string, int>();
            Dictionary<string, int> recordTypeCounts = new Dictionary<string, int>();

            //
            // Walk repo
            //
            Repo.WalkRepo(
                repoFile,
                (repoHeader) =>
                {
                    Logger.LogTrace($"headerJson: {repoHeader.JsonString}");
                    return true;
                },
                (repoRecord) =>
                {
                    Logger.LogTrace($"cid: {repoRecord.Cid.GetBase32()}");
                    Logger.LogTrace($"blockJson: {repoRecord.JsonString}");

                    // For stats
                    totalRecords++;
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
            Logger.LogInfo($"TOTAL RECORDS:");
            Logger.LogInfo($"   {totalRecords}");
            Logger.LogTrace($"DAG CBOR TYPE COUNTS:");
            foreach (var kvp in dagCborTypeCounts)
            {
                Logger.LogTrace($"  {kvp.Key} - {kvp.Value}");
            }
            Logger.LogInfo($"RECORD TYPE COUNTS:");
            // print in order of most common to least common
            foreach (var kvp in recordTypeCounts.OrderByDescending(kvp => kvp.Value))
            {
                Logger.LogInfo($"  {kvp.Key} - {kvp.Value}");
            }
        }
   }
}