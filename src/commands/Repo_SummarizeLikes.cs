using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using dnproto.utils;

namespace dnproto.commands
{
    public class Repo_SummarizeLikes : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"repoFile"});
        }

        public override HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>(new string[]{"verbose", "printcount", "createdafter", "resolvehandles", "resolvewaitseconds"});
        }

        /// <summary>
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? repoFile = CommandLineInterface.GetArgumentValue(arguments, "repoFile");
            string? verboseS = CommandLineInterface.GetArgumentValue(arguments, "verbose");
            bool verbose = string.IsNullOrEmpty(verboseS) == false && string.Equals(verboseS, "true");
            int printCount = CommandLineInterface.GetArgumentValueWithDefault(arguments, "printcount", 20);
            string createdAfter = CommandLineInterface.GetArgumentValueWithDefault(arguments, "createdafter", "2010");
            bool resolveHandles = CommandLineInterface.GetArgumentValueWithDefault(arguments, "resolvehandles", false);
            int resolveWaitSeconds = CommandLineInterface.GetArgumentValueWithDefault(arguments, "resolvewaitseconds", 5);

            if (string.IsNullOrEmpty(repoFile))
            {
                Console.WriteLine("repoFile is empty.");
                return;
            }

            bool fileExists = File.Exists(repoFile);

            Console.WriteLine($"repoFile: {repoFile}");
            Console.WriteLine($"fileExists: {fileExists}");
            Console.WriteLine($"verbose: {verbose}");
            Console.WriteLine($"printCount: {printCount}");
            Console.WriteLine($"createdAfter: {createdAfter}");
            Console.WriteLine($"resolveHandles: {resolveHandles}");
            Console.WriteLine($"resolveWaitSeconds: {resolveWaitSeconds}");
            Console.WriteLine($"");

            if (!fileExists)
            {
                Console.WriteLine("File does not exist.");
                return;
            }

            Dictionary<string, int> likeCountsByDid = new Dictionary<string, int>();


            using(var fs = new FileStream(repoFile, FileMode.Open))
            {
                //
                // Read header
                //
                VarInt headerLength = VarInt.ReadVarInt(fs);
                var header = DagCborObject.ReadFromStream(fs);

                // print
                var headerJson = JsonData.GetObjectJsonString(header.GetRawValue());

                if(verbose)
                {
                    Console.WriteLine();
                    Console.WriteLine($"headerJson:");
                    Console.WriteLine();
                    Console.WriteLine($"{headerJson}");
                    Console.WriteLine();
                }


                while(fs.Position < fs.Length)
                { 
                    if(verbose) Console.WriteLine($" -----------------------------------------------------------------------------------------------------------");

                    //
                    // Read data block (record)
                    //
                    VarInt blockLength = VarInt.ReadVarInt(fs);
                    CidV1 cid = CidV1.ReadCid(fs);
                    var dataBlock = DagCborObject.ReadFromStream(fs);


                    // print
                    if(verbose)
                    {
                        Console.WriteLine($"cid: {cid.GetBase32()}");
                        Console.WriteLine();
                        var blockJson = JsonData.GetObjectJsonString(dataBlock.GetRawValue());
                        Console.WriteLine();
                        Console.WriteLine($"blockJson:");
                        Console.WriteLine();
                        Console.WriteLine($"{blockJson}");
                        Console.WriteLine();
                    }

                    var recordType = dataBlock.GetMapValueAtPath(new string[]{"$type"});
                    var createdAt = dataBlock.GetMapValueAtPath(new string[]{"createdAt"});

                    if (string.IsNullOrEmpty(recordType) == false 
                        && string.Equals(recordType, "app.bsky.feed.like") 
                        && string.IsNullOrEmpty(createdAt) == false && string.Compare(createdAt, createdAfter) > 0)
                    {
                        var uri = dataBlock.GetMapValueAtPath(new string[]{"subject", "uri"});

                        if(verbose)
                        {
                            Console.WriteLine();
                            Console.WriteLine($"recordType: {recordType}");
                            Console.WriteLine($"createdAt: {createdAt}");
                            Console.WriteLine($"uri: {uri}");
                        }

                        if (uri != null)
                        {
                            var uriParts = uri.Split('/');
                            var likeDid = uriParts.Count() > 2 ? uriParts[2] : null;

                            if(verbose) Console.WriteLine($"likeDid: {likeDid}");

                            if(string.IsNullOrEmpty(likeDid) == false)
                            {
                                if(likeCountsByDid.ContainsKey(likeDid))
                                {
                                    likeCountsByDid[likeDid] = likeCountsByDid[likeDid] + 1;
                                }
                                else
                                {
                                    likeCountsByDid[likeDid] = 1;
                                }
                            }
                        }

                        if(verbose) Console.WriteLine();
                    }
                }
            }


            // Get list sorted by count
            var sortedLikeCountsByDid = likeCountsByDid.OrderByDescending(x => x.Value);
            Console.WriteLine("dict length: " + sortedLikeCountsByDid.Count());


            // Print top n
            int i = 0;
            int sumOfTopLikes = 0;
   
            foreach (var kvp in sortedLikeCountsByDid)
            {
                i++;
                sumOfTopLikes += kvp.Value;
                var profileUrl = $"https://bsky.app/profile/{kvp.Key}";
                Console.WriteLine($"{profileUrl}: {kvp.Value}");

                if(i >= printCount)
                {
                    break;
                }
            }

            // print sum of dictionary values
            Console.WriteLine();
            Console.WriteLine("sum of top likes: " + sumOfTopLikes);
            Console.WriteLine("total number of likes: " + sortedLikeCountsByDid.Sum(x => x.Value));
            Console.WriteLine();



            // Resolving handles?
            if(resolveHandles)
            {
                i = 0;
                Console.WriteLine();
                Console.WriteLine("Resolving handles...");
                Console.WriteLine();

                foreach (var kvp in sortedLikeCountsByDid)
                {
                    i++;

                    JsonNode? profile = Profile_Get.DoGetProfile(kvp.Key);

                    string handle = JsonData.GetPropertyValue(profile, "handle");

                    var profileUrl = $"https://bsky.app/profile/{kvp.Key}";
                    Console.WriteLine($"{profileUrl}: {kvp.Value}   ({handle})");


                    // sleep 2 secs
                    Thread.Sleep(resolveWaitSeconds * 1000);

                    if(i >= printCount)
                    {
                        break;
                    }
                }
            }
        }
   }
}