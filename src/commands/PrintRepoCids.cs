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
                return $"(VarInt length:{Length} value:{Value} hex:0x{Value:X})";
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
                Console.WriteLine($"headerLength: {headerLength}");
                Console.WriteLine($"headerBytesRead: {headerBytesRead}");
                Console.WriteLine();

                while(fs.Position < fs.Length)
                { 
                    Console.WriteLine($" -----------------------------------------------------------------------------------------------------------");
                    VarInt blockLength = ReadVarInt(fs);

                    Console.WriteLine($"blockLength: {blockLength}");
                    Console.WriteLine();

                    // cid

                    // https://github.com/multiformats/cid
                    VarInt cidVersion = ReadVarInt(fs);
                    VarInt cidMulticodec = ReadVarInt(fs);
                    VarInt cidHashFunction = ReadVarInt(fs); // likely sha2-256, 0x12, decimal 18
                    VarInt cidDigestSize = ReadVarInt(fs);

                    Console.WriteLine($"cidVersion: {cidVersion}");
                    Console.WriteLine($"cidMulticodec: {cidMulticodec}");
                    Console.WriteLine($"cidHashFunction: {cidHashFunction}");
                    Console.WriteLine($"cidDigestSize: {cidDigestSize}");

                    // https://github.com/multiformats/multicodec/blob/master/table.csv
                    // dag-cbor = 0x71
                    if(cidMulticodec.Value != 0x71)
                    {
                        Console.WriteLine($"cidMulticodec.Value != 0x71: {cidMulticodec.Value}");
                    }

                    byte[] cidDigestBytes = new byte[cidDigestSize.Value];
                    int cidDigestBytesRead = fs.Read(cidDigestBytes, 0, cidDigestSize.Value);

                    // Put full cid together
                    var ms = new MemoryStream();
                    ms.WriteByte((byte)cidVersion.Value);
                    ms.WriteByte((byte)cidMulticodec.Value);
                    ms.WriteByte((byte)cidHashFunction.Value);
                    ms.WriteByte((byte)cidDigestSize.Value);
                    ms.Write(cidDigestBytes, 0, cidDigestSize.Value);
                    byte[] cidBytes = ms.ToArray();
                    int cidBytesLength = cidBytes.Length;

                    string cidBits = string.Join("", cidBytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
                    string cidbase32 = "b" + BytesToBase32(cidBytes);
                    Console.WriteLine($"cidBytesLength: {cidBytesLength}");
                    Console.WriteLine($"cidBits: {cidBits}");
                    Console.WriteLine($"cidbase32: {cidbase32}");
                    Console.WriteLine();

                    // rest of data block
                    int restOfBlockLength = blockLength.Value - (cidVersion.Length + cidMulticodec.Length + cidHashFunction.Length + cidDigestSize.Length + cidDigestSize.Value);
                    byte[] blockBytes = new byte[restOfBlockLength];
                    int blockBytesRead = fs.Read(blockBytes, 0, restOfBlockLength);

                    Console.WriteLine($"restOfBlockLength: {restOfBlockLength}");
                    Console.WriteLine($"blockBytesRead: {blockBytesRead}");
                    Console.WriteLine();
                    Console.WriteLine($"blockBytes string:");
                    Console.WriteLine($"{Encoding.UTF8.GetString(blockBytes)}");
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

        //
        // Clearly not the most efficient way to do this, but it works for now.
        //
        // Take 5 bits at a time, convert to base32 character.
        //
        public static string BytesToBase32(byte[] bytes)
        {
            string base32characters = "abcdefghijklmnopqrstuvwxyz234567";
            string cidBits = string.Join("", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));

            int index = 0;

            StringBuilder sb = new StringBuilder();

            while(index < cidBits.Length-5)
            {
                string next5 = cidBits.Substring(index, 5);
                index += 5;
                int next5Int = Convert.ToInt32(next5, 2);
                char next5Char = base32characters[next5Int];
                sb.Append(next5Char);
            }

            if (index < cidBits.Length)
            {
                string next5 = cidBits.Substring(index).PadRight(5, '0');
                int next5Int = Convert.ToInt32(next5, 2);
                char next5Char = base32characters[next5Int];
                sb.Append(next5Char);
            }

            return sb.ToString();
        }
    }
}