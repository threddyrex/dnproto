using System.Text;

using dnproto.utils;

namespace dnproto.commands
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


            using(var fs = new FileStream(repoFile, FileMode.Open))
            {
                //
                // Read header
                //
                RepoHeader repoHeader = RepoHeader.ReadFromStream(fs);

                // print
                Console.WriteLine();
                Console.WriteLine($"headerJson:");
                Console.WriteLine();
                Console.WriteLine($"{repoHeader.JsonString}");
                Console.WriteLine();


                while(fs.Position < fs.Length)
                { 
                    Console.WriteLine($" -----------------------------------------------------------------------------------------------------------");

                    //
                    // Read data block (record)
                    //
                    var repoRecord = RepoRecord.ReadFromStream(fs);

                    // print
                    Console.WriteLine($"cid: {repoRecord.Cid.GetBase32()}");
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine($"blockJson:");
                    Console.WriteLine();
                    Console.WriteLine($"{repoRecord.JsonString}");
                    Console.WriteLine();
                }
            }
        }
   }
}