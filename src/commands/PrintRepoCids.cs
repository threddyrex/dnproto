using System.Text;

using dnproto.utils;

namespace dnproto.commands
{
    public class PrintRepoCids : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"repoFile"});
        }

        /// <summary>
        /// Load repo car and list cids.
        ///
        /// https://ipld.io/specs/transport/car/carv1/#format-description
        /// 
        /// [---  header  --------]   [----------------- data ---------------------------------]
        /// [varint | header block]   [varint | cid | data block]....[varint | cid | data block] 
        ///
        /// 
        /// represented using the data types we have:
        /// 
        /// [---  header  --------]   [----------------- data ---------------------------------]
        /// [VarInt | CborObject  ]   [VarInt | Cid | CborObject]....[VarInt | Cid | CborObject] 
        ///
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
                VarInt headerLength = VarInt.ReadVarInt(fs);
                var header = CborObject.ReadFromStream(fs);
                var headerJson = JsonData.GetObjectJsonString(header.GetRawValue());

                Console.WriteLine();
                Console.WriteLine($"headerJson:");
                Console.WriteLine();
                Console.WriteLine($"{headerJson}");
                Console.WriteLine();


                while(fs.Position < fs.Length)
                { 
                    Console.WriteLine($" -----------------------------------------------------------------------------------------------------------");

                    // Read block length
                    VarInt blockLength = VarInt.ReadVarInt(fs);

                    // Read cid
                    Cid cid = Cid.ReadCid(fs);
                    Console.WriteLine($"cid: {cid.GetBase32()}");
                    Console.WriteLine();

                    // rest of data block
                    var dataBlock = CborObject.ReadFromStream(fs);
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