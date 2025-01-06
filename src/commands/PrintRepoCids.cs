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

            int cidCount = 0;

            using(var fs = new FileStream(repoFile, FileMode.Open))
            {
                VarInt headerLength = VarInt.ReadVarInt(fs);

                byte[] headerBytes = new byte[headerLength.Value];
                int headerBytesRead = fs.Read(headerBytes, 0, headerLength.Value);

                Console.WriteLine();
                Console.WriteLine($"headerLength: {headerLength}");
                Console.WriteLine($"headerBytesRead: {headerBytesRead}");
                Console.WriteLine();

                using(var ms = new MemoryStream(headerBytes))
                {
                    var header = CborObject.ReadFromStream(ms);
                    Console.WriteLine($"header: {header}");
                }


                while(fs.Position < fs.Length)
                { 
                    Console.WriteLine($" -----------------------------------------------------------------------------------------------------------");

                    // Full block length
                    VarInt blockLength = VarInt.ReadVarInt(fs);
                    Console.WriteLine($"blockLength: {blockLength}");
                    Console.WriteLine();

                    // cid
                    Cid cid = Cid.ReadCid(fs);

                    Console.WriteLine($"cidVersion:       {cid.Version}");
                    Console.WriteLine($"cidMulticodec:    {cid.Multicodec}");
                    Console.WriteLine($"cidHashFunction:  {cid.HashFunction}");
                    Console.WriteLine($"cidDigestSize:    {cid.DigestSize}");

                    byte[] cidBytes = cid.GetBytes();
                    int cidBytesLength = cidBytes.Length;
                    string cidbase32 = cid.GetBase32();

                    Console.WriteLine($"cidBytesLength:   {cidBytesLength}");
                    Console.WriteLine($"cidbase32 NEW:    {cidbase32}");
                    Console.WriteLine();

                    cidCount++;

                    // rest of data block
                    int restOfBlockLength = blockLength.Value - (cid.Version.Length + cid.Multicodec.Length + cid.HashFunction.Length + cid.DigestSize.Length + cid.DigestSize.Value);
                    byte[] blockBytes = new byte[restOfBlockLength];
                    int blockBytesRead = fs.Read(blockBytes, 0, restOfBlockLength);

                    Console.WriteLine($"restOfBlockLength: {restOfBlockLength}");
                    Console.WriteLine($"blockBytesRead: {blockBytesRead}");
                    Console.WriteLine();
                    Console.WriteLine($"blockBytes string:");
                    Console.WriteLine($"{Encoding.UTF8.GetString(blockBytes)}");
                    Console.WriteLine();

                    using(var ms = new MemoryStream(blockBytes))
                    {
                        var block = CborObject.ReadFromStream(ms);
                        Console.WriteLine($"block: {block}");
                    }
                }
            }

            Console.WriteLine($"cidCount: {cidCount}");;
        }
   }
}