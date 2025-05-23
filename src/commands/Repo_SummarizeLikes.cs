using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using dnproto.repo;
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

            //
            // Walk repo
            //
            Repo.WalkRepo(
                repoFile,
                (repoHeader) => { return true; },
                (repoRecord) =>
                {
                    if (string.Equals(repoRecord.RecordType, "app.bsky.feed.like") == false) return true;
                    if (string.IsNullOrEmpty(repoRecord.CreatedAt)) return true;
                    if ((string.Compare(repoRecord.CreatedAt, createdAfter) > 0) == false) return true;

                    string? uri = repoRecord.DataBlock.SelectString(["subject", "uri"]);
                    if (uri == null) return true;

                    var uriParts = uri.Split('/');
                    var likeDid = uriParts.Length > 2 ? uriParts[2] : null;
                    if (string.IsNullOrEmpty(likeDid)) return true;

                    if (likeCountsByDid.ContainsKey(likeDid))
                    {
                        likeCountsByDid[likeDid] = likeCountsByDid[likeDid] + 1;
                    }
                    else
                    {
                        likeCountsByDid[likeDid] = 1;
                    }

                    return true;
                }
            );



            //
            // Get list sorted by count
            //
            var sortedLikeCountsByDid = likeCountsByDid.OrderByDescending(x => x.Value);
            Console.WriteLine("number of accounts: " + sortedLikeCountsByDid.Count());

            Console.WriteLine();


            //
            // Print top n
            //
            int i = 0;
            int sumOfTopLikes = 0;
   
            foreach (var kvp in sortedLikeCountsByDid)
            {
                i++;
                sumOfTopLikes += kvp.Value;


                if(resolveHandles)
                {
                    JsonNode? profile = Profile_Get.DoGetProfile(kvp.Key);
                    string? handle = JsonData.SelectString(profile, "handle");

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