using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using dnproto.repo;
using dnproto.utils;

namespace dnproto.commands
{
    public class Repo_Compare : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"repoFile1", "repoFile2"});
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
            string? repoFile1 = CommandLineInterface.GetArgumentValue(arguments, "repoFile1");
            string? repoFile2 = CommandLineInterface.GetArgumentValue(arguments, "repoFile2");

            Console.WriteLine($"repoFile1: {repoFile1}");
            Console.WriteLine($"repoFile2: {repoFile2}");

            //
            // Validate arguments
            //
            if (string.IsNullOrEmpty(repoFile1))
            {
                Console.WriteLine("repoFile1 is empty.");
                return;
            }

            if (string.IsNullOrEmpty(repoFile2))
            {
                Console.WriteLine("repoFile2 is empty.");
                return;
            }

            bool fileExists1 = File.Exists(repoFile1);
            bool fileExists2 = File.Exists(repoFile2);


            if (!fileExists1)
            {
                Console.WriteLine("File1 does not exist.");
                return;
            }

            if (!fileExists2)
            {
                Console.WriteLine("File2 does not exist.");
                return;
            }


            Console.WriteLine($"");



            //
            // Find did for repos
            //
            string? did1 = Repo.FindDid(repoFile1);
            string? did2 = Repo.FindDid(repoFile2);

            Console.WriteLine($"did1: {did1}");
            Console.WriteLine($"did2: {did2}");
            Console.WriteLine($"");

            if(string.IsNullOrEmpty(did1) || string.IsNullOrEmpty(did2))
            {
                Console.WriteLine("Did not find did for repo1 or repo2.");
                return;
            }


            //
            // repo1 --> did2
            //
            Dictionary<string, string>? rkeys1 = Repo.FindRkeys(repoFile1);
            if (rkeys1 == null)
            {
                Console.WriteLine("Could not find merkle records for repo1.");
                return;
            }
            CommandLineInterface.PrintLineSeparator();
            Console.WriteLine($"-------------------------------------------------");
            Console.WriteLine($"| FIRST REPO                                    |");
            Console.WriteLine($"-------------------------------------------------");
            Console.WriteLine($"");
            Console.WriteLine($"USER:   {did1}");
            Console.WriteLine($"");
            Console.WriteLine($"");
            FindLikesForRepo(repoFile1, did1, did2);
            Console.WriteLine($"");
            FindRepliesForRepo(repoFile1, did1, did2);
            Console.WriteLine($"");
            FindRepostsForRepo(repoFile1, did1, did2);
            Console.WriteLine($"");
            FindQuotePostsForRepo(repoFile1, did1, did2);
            Console.WriteLine($"");
            FindMentionsForRepo3(repoFile1, did1, did2, rkeys1);
            Console.WriteLine($"");


            //
            // repo2 --> did1
            //
            Dictionary<string, string>? rkeys2 = Repo.FindRkeys(repoFile2);
            if (rkeys2 == null)
            {
                Console.WriteLine("Could not find merkle records for repo2.");
                return;
            }
            CommandLineInterface.PrintLineSeparator();
            Console.WriteLine($"-------------------------------------------------");
            Console.WriteLine($"| SECOND REPO                                   |");
            Console.WriteLine($"-------------------------------------------------");
            Console.WriteLine($"");
            Console.WriteLine($"USER:   {did2}");
            Console.WriteLine($"");
            Console.WriteLine($"");
            FindLikesForRepo(repoFile2, did2, did1);
            Console.WriteLine($"");
            FindRepliesForRepo(repoFile2, did2, did1);
            Console.WriteLine($"");
            FindRepostsForRepo(repoFile2, did2, did1);
            Console.WriteLine($"");
            FindQuotePostsForRepo(repoFile2, did2, did1);
            Console.WriteLine($"");
            FindMentionsForRepo3(repoFile2, did2, did1, rkeys2);
            Console.WriteLine($"");


        }



        public static void FindLikesForRepo(string? repoFile, string? sourceDid, string? targetDid)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid)) return;

            Repo.WalkRepo(
                repoFile,
                (header) => { return true;}, 
                (record) => 
                {
                    if (record.RecordType != "app.bsky.feed.like") return true;

                    string? uri = record.DataBlock.SelectString(["subject", "uri"]);
                    if (uri == null || uri.Contains(targetDid) == false) return true;

                    string clickableUri = uri.Replace("at://", "https://bsky.app/profile/").Replace("app.bsky.feed.post/", "post/");
                    Console.WriteLine($"liked -->   {clickableUri}");

                    return true;
                }
            );
        }


        public static void FindRepliesForRepo(string? repoFile, string? sourceDid, string? targetDid)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid)) return;

            Repo.WalkRepo(
                repoFile,
                (header) => { return true;}, 
                (record) => 
                {
                    if (record.RecordType != "app.bsky.feed.post") return true;

                    string? uriReply = record.DataBlock.SelectString(["reply", "parent", "uri"]);
                    string? uriRoot = record.DataBlock.SelectString(["reply", "root", "uri"]);

                    if (uriReply != null && uriReply.Contains(targetDid))
                    {
                        string clickableUri = uriReply.Replace("at://", "https://bsky.app/profile/").Replace("app.bsky.feed.post/", "post/");
                        Console.WriteLine($"replied to -->   {clickableUri}");
                    }
                    else if (uriRoot != null && uriRoot.Contains(targetDid))
                    {
                        string clickableUri = uriRoot.Replace("at://", "https://bsky.app/profile/").Replace("app.bsky.feed.post/", "post/");
                        Console.WriteLine($"replied to -->   {clickableUri}");
                    }

                    return true;
                }
            );
        }


        
        public static void FindRepostsForRepo(string? repoFile, string? sourceDid, string? targetDid)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid)) return;

            Repo.WalkRepo(
                repoFile,
                (header) => { return true;}, 
                (record) => 
                {
                    if (record.RecordType != "app.bsky.feed.repost") return true;

                    string? uri = record.DataBlock.SelectString(["subject", "uri"]);
                    if (uri == null || uri.Contains(targetDid) == false) return true;

                    string clickableUri = uri.Replace("at://", "https://bsky.app/profile/").Replace("app.bsky.feed.post/", "post/");
                    Console.WriteLine($"reposted -->   {clickableUri}");

                    return true;
                }
            );
        }


        
        public static void FindQuotePostsForRepo(string? repoFile, string? sourceDid, string? targetDid)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid)) return;

            Repo.WalkRepo(
                repoFile,
                (header) => { return true;}, 
                (record) => 
                {
                    if (record.RecordType != "app.bsky.feed.post") return true;

                    string? uri = record.DataBlock.SelectString(["embed", "record", "uri"]);
                    if (uri == null || uri.Contains(targetDid) == false) return true;

                    string clickableUri = uri.Replace("at://", "https://bsky.app/profile/").Replace("app.bsky.feed.post/", "post/");
                    Console.WriteLine($"quote posted -->   {clickableUri}");

                    return true;
                }
            );
        }


        

        public static void FindMentionsForRepo3(string? repoFile, string? sourceDid, string? targetDid, Dictionary<string, string>? rkeys)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid) || rkeys == null) return;

            Repo.WalkRepo(
                repoFile,
                (header) => { return true;}, 
                (record) => 
                {
                    if (record.RecordType != "app.bsky.feed.post") return true;

                    string? text = record.DataBlock.SelectString(["text"]);
                    if (text == null) return true;

                    List<DagCborObject>? facets = record.DataBlock.SelectObject(["facets"]) as List<DagCborObject>;
                    if (facets == null) return true;

                    foreach(DagCborObject facet in facets)
                    {
                        List<DagCborObject>? featuresArray = facet.SelectObject(["features"]) as List<DagCborObject>;
                        if (featuresArray == null) return true;
                        if (featuresArray.Count != 1) return true;

                        DagCborObject feature = featuresArray[0];
                        string? did = feature.SelectString(["did"]);
                        string? type = feature.SelectString(["$type"]);

                        if (type != "app.bsky.richtext.facet#mention" || did != targetDid) return true;

                        string? rkey = rkeys.ContainsKey(record.Cid.Base32) ? rkeys[record.Cid.Base32] : null;

                        if (rkey == null)
                        {
                            Console.WriteLine($"------------------------");
                            Console.WriteLine($"| mentioned (no rkey)  |");
                            Console.WriteLine($"------------------------");
                            Console.WriteLine($"{text}");
                            Console.WriteLine("");
                        }
                        else
                        {
                            string clickableUri = $"https://bsky.app/profile/{sourceDid}/post/{rkey}";
                            Console.WriteLine($"mentioned -->   {clickableUri}");
                        }
                    }

                    return true;
                }
            );

        }


   }
}