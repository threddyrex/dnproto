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
                Console.WriteLine("repoFile is empty.");
                return;
            }

            bool fileExists = File.Exists(repoFile);

            Console.WriteLine($"repoFile: {repoFile}");
            Console.WriteLine($"fileExists: {fileExists}");

            if (!fileExists)
            {
                Console.WriteLine("File does not exist.");
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
                    Console.WriteLine($"headerJson:");
                    Console.WriteLine();
                    Console.WriteLine($"{repoHeader.JsonString}");
                    Console.WriteLine();
                    return true;
                },
                (repoRecord) =>
                {
                    Console.WriteLine($" -----------------------------------------------------------------------------------------------------------");
                    Console.WriteLine($"cid: {repoRecord.Cid.GetBase32()}");
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine($"blockJson:");
                    Console.WriteLine();
                    Console.WriteLine($"{repoRecord.JsonString}");
                    Console.WriteLine();

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
            Console.WriteLine($" -----------------------------------------------------------------------------------------------------------");
            Console.WriteLine($"Stats:");
            Console.WriteLine($" -----------------------------------------------------------------------------------------------------------");
            Console.WriteLine($"Total records: {totalRecords}");
            Console.WriteLine();
            Console.WriteLine($"DagCborType counts:");
            foreach (var kvp in dagCborTypeCounts)
            {
                Console.WriteLine($"    {kvp.Key} - {kvp.Value}");
            }
            Console.WriteLine();
            Console.WriteLine($"RecordType counts:");
            foreach (var kvp in recordTypeCounts)
            {
                Console.WriteLine($"    {kvp.Key} - {kvp.Value}");
            }
        }
   }
}