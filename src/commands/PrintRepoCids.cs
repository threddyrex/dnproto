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
                VarInt headerLength = VarInt.ReadVarInt(fs);
                byte[] headerBytes = new byte[headerLength.Value];
                int headerBytesRead = fs.Read(headerBytes, 0, headerLength.Value);

                Console.WriteLine();
                Console.WriteLine($"headerLength: {headerLength}");
                Console.WriteLine($"headerBytesRead: {headerBytesRead}");
                Console.WriteLine();

                while(fs.Position < fs.Length)
                { 
                    Console.WriteLine($" -----------------------------------------------------------------------------------------------------------");

                    // Full block length
                    VarInt blockLength = VarInt.ReadVarInt(fs);
                    Console.WriteLine($"blockLength: {blockLength}");
                    Console.WriteLine();

                    // cid

                    // https://github.com/multiformats/cid
                    VarInt cidVersion = VarInt.ReadVarInt(fs);
                    VarInt cidMulticodec = VarInt.ReadVarInt(fs);
                    VarInt cidHashFunction = VarInt.ReadVarInt(fs); // likely sha2-256, 0x12, decimal 18
                    VarInt cidDigestSize = VarInt.ReadVarInt(fs);

                    Console.WriteLine($"cidVersion:       {cidVersion}");
                    Console.WriteLine($"cidMulticodec:    {cidMulticodec}");
                    Console.WriteLine($"cidHashFunction:  {cidHashFunction}");
                    Console.WriteLine($"cidDigestSize:    {cidDigestSize}");

                    // https://github.com/multiformats/multicodec/blob/master/table.csv
                    // dag-cbor = 0x71
                    // should not happen for AT
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
                    string cidbase32 = "b" + Base32Encoding.BytesToBase32(cidBytes);
                    string cidbase32Orig = "b" + Base32Encoding.BytesToBase32Orig(cidBytes);
                    Console.WriteLine($"cidBytesLength:   {cidBytesLength}");
                    //Console.WriteLine($"cidBits: {cidBits}");
                    Console.WriteLine($"cidbase32 OLD:    {cidbase32Orig}");
                    Console.WriteLine($"cidbase32 NEW:    {cidbase32}");
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

   }
}