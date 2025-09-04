using System.Text;

using dnproto.repo;

namespace dnproto.cli.commands
{
    public class Repo_PrintRecords : BaseCommand
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
                Logger.LogError("repoFile is empty.");
                return;
            }

            bool fileExists = File.Exists(repoFile);

            Logger.LogTrace($"repoFile: {repoFile}");
            Logger.LogTrace($"fileExists: {fileExists}");

            if (!fileExists)
            {
                Logger.LogError("File does not exist.");
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
                    Logger.LogInfo($"headerJson: {repoHeader.JsonString}");
                    return true;
                },
                (repoRecord) =>
                {
                    Logger.LogInfo($"cid: {repoRecord.Cid.GetBase32()}");
                    Logger.LogInfo($"blockJson: {repoRecord.JsonString}");

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

                    string recordType = repoRecord.RecordType ?? "null";

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
            Logger.LogInfo($"Stats:");
            Logger.LogInfo($"Total records: {totalRecords}");
            Logger.LogInfo($"");
            Logger.LogInfo($"DagCborType counts:");
            foreach (var kvp in dagCborTypeCounts)
            {
                Logger.LogInfo($"    {kvp.Key} - {kvp.Value}");
            }
            Logger.LogInfo($"RecordType counts:");
            foreach (var kvp in recordTypeCounts)
            {
                Logger.LogInfo($"    {kvp.Key} - {kvp.Value}");
            }
        }
   }
}