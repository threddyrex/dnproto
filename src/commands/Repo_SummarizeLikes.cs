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
            return new HashSet<string>(new string[]{"printcount", "createdafter", "resolvehandles", "resolvewaitseconds"});
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
                var repoHeader = RepoHeader.ReadFromStream(fs);


                while(fs.Position < fs.Length)
                { 
                    //
                    // Read data block (record)
                    //
                    var repoRecord = RepoRecord.ReadFromStream(fs);

                    //
                    // validate
                    //
                    if (string.Equals(repoRecord.RecordType, "app.bsky.feed.like") == false) continue;
                    if (string.IsNullOrEmpty(repoRecord.CreatedAt)) continue;
                    if ((string.Compare(repoRecord.CreatedAt, createdAfter) > 0) == false) continue;

                    string? uri = repoRecord.DataBlock.SelectString(["subject", "uri"]);
                    if (uri == null) continue;

                    var uriParts = uri.Split('/');
                    var likeDid = uriParts.Count() > 2 ? uriParts[2] : null;
                    if (string.IsNullOrEmpty(likeDid)) continue;


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


            // Get list sorted by count
            var sortedLikeCountsByDid = likeCountsByDid.OrderByDescending(x => x.Value);
            Console.WriteLine("number of accounts: " + sortedLikeCountsByDid.Count());

            Console.WriteLine();

            // Print top n
            int i = 0;
            int sumOfTopLikes = 0;
   
            foreach (var kvp in sortedLikeCountsByDid)
            {
                i++;
                sumOfTopLikes += kvp.Value;


                if(resolveHandles)
                {
                    JsonNode? profile = Profile_Get.DoGetProfile(kvp.Key);
                    string handle = JsonData.GetPropertyValue(profile, "handle");

                    var profileUrl = $"https://bsky.app/profile/{kvp.Key}";
                    Console.WriteLine($"{profileUrl}   {kvp.Value} likes     ({handle})");

                    // sleep 2 secs
                    Thread.Sleep(resolveWaitSeconds * 1000);

                }
                else
                {
                    var profileUrl = $"https://bsky.app/profile/{kvp.Key}";
                    Console.WriteLine($"{profileUrl}   {kvp.Value} likes");
                }


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

        }
   }
}