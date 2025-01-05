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

        public class VarInt
        {
            public int Length { get; set; }
            public int Value { get; set; }

            public override string ToString()
            {
                return $"[length: {Length}, value: {Value}]";
            }
        }

        /// <summary>
        /// Load repo car and list cids
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
                VarInt headerLength = ReadVarInt(fs);
                byte[] headerBytes = new byte[headerLength.Value];
                int headerBytesRead = fs.Read(headerBytes, 0, headerLength.Value);

                Console.WriteLine();
                Console.WriteLine($"headerLength: {headerLength}  headerBytesRead: {headerBytesRead}");
                Console.WriteLine();

                while(fs.Position < fs.Length)
                { 
                    Console.WriteLine($" -----------------------------------------------------------------------------------------------------------");
                    VarInt blockLength = ReadVarInt(fs);

                    // https://github.com/multiformats/cid
                    VarInt cidVersion = ReadVarInt(fs);
                    VarInt cidMulticodec = ReadVarInt(fs);

                    // https://github.com/multiformats/multicodec/blob/master/table.csv
                    // dag-cbor = 0x71
                    if(cidMulticodec.Value != 0x71)
                    {
                        Console.WriteLine($"cidMulticodec.Value != 0x71: {cidMulticodec.Value}");
                    }

                    // cid
                    int CID_LENGTH = 36;                    
                    byte[] cidBytes = new byte[CID_LENGTH];
                    int cidBytesRead = fs.Read(cidBytes, 0, CID_LENGTH);

                    string cid = BitConverter.ToString(cidBytes).Replace("-", string.Empty);
                    Console.WriteLine($"cidBytesRead: {cidBytesRead}  cid: {cid}");
                    Console.WriteLine();

                    // rest of data block
                    int restOfBlockLength = blockLength.Value - (cidVersion.Length + cidMulticodec.Length + CID_LENGTH);
                    byte[] blockBytes = new byte[restOfBlockLength];
                    int blockBytesRead = fs.Read(blockBytes, 0, restOfBlockLength);

                    Console.WriteLine($"blockLength: {blockLength}  cidVersion: {cidVersion}  cidMulticodec: {cidMulticodec}  restOfBlockLength: {restOfBlockLength}  blockBytesRead: {blockBytesRead}");
                    Console.WriteLine();
                    Console.WriteLine($"blockBytes string: {Encoding.UTF8.GetString(blockBytes)}");
                    Console.WriteLine();
                }
            }
        }

        public static VarInt ReadVarInt(FileStream fs)
        {
            VarInt ret = new VarInt();
            ret.Value = 0;
            ret.Length = 0;

            int shift = 0;
            byte b;
            do
            {
                ret.Length++;
                b = (byte)fs.ReadByte();
                ret.Value |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return ret;
        }
    }
}