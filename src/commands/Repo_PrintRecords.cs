using System.Text;

using dnproto.utils;

namespace dnproto.commands
{
    public class Repo_PrintRecords : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"repoFile"});
        }

        /// <summary>
        /// Load repo car and list cids.
        ///
        /// Format from spec:
        /// 
        ///    [---  header  --------]   [----------------- data ---------------------------------]
        ///    [varint | header block]   [varint | cid | data block]....[varint | cid | data block] 
        ///
        /// 
        /// represented using the data types we have:
        /// 
        ///    [---  header  --------]   [----------------- data ---------------------------------]
        ///    [VarInt | CborObject  ]   [VarInt | Cid | CborObject]....[VarInt | Cid | CborObject] 
        ///
        /// 
        /// https://ipld.io/specs/transport/car/carv1/#format-description
        /// 
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
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
                VarInt headerLength = VarInt.ReadVarInt(fs);
                var header = DagCborObject.ReadFromStream(fs);

                // print
                var headerJson = JsonData.GetObjectJsonString(header.GetRawValue());
                Console.WriteLine();
                Console.WriteLine($"headerJson:");
                Console.WriteLine();
                Console.WriteLine($"{headerJson}");
                Console.WriteLine();


                while(fs.Position < fs.Length)
                { 
                    Console.WriteLine($" -----------------------------------------------------------------------------------------------------------");

                    //
                    // Read data block (record)
                    //
                    VarInt blockLength = VarInt.ReadVarInt(fs);
                    CidV1 cid = CidV1.ReadCid(fs);
                    var dataBlock = DagCborObject.ReadFromStream(fs);


                    // print
                    Console.WriteLine($"cid: {cid.GetBase32()}");
                    Console.WriteLine();
                    var blockJson = JsonData.GetObjectJsonString(dataBlock.GetRawValue());
                    Console.WriteLine();
                    Console.WriteLine($"blockJson:");
                    Console.WriteLine();
                    Console.WriteLine($"{blockJson}");
                    Console.WriteLine();
                }
            }
        }
   }
}