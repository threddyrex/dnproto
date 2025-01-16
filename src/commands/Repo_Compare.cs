using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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
            string? did1 = FindDidForRepo(repoFile1);
            string? did2 = FindDidForRepo(repoFile2);

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
            Dictionary<string, DagCborObject>? merkle1 = FindMerkleRecords(repoFile1);
            if (merkle1 == null)
            {
                Console.WriteLine("Could not find merkle records for repo1.");
                return;
            }
            CommandLineInterface.PrintLineSeparator();
            Console.WriteLine($"-------------------------------------------------");
            Console.WriteLine($"| FIRST REPO                                    |");
            Console.WriteLine($"-------------------------------------------------");
            Console.WriteLine($"");
            Console.WriteLine($"repo:   {did1}");
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
            FindMentionsForRepo3(repoFile1, did1, did2, merkle1);
            Console.WriteLine($"");


            //
            // repo2 --> did1
            //
            Dictionary<string, DagCborObject>? merkle2 = FindMerkleRecords(repoFile2);
            if (merkle2 == null)
            {
                Console.WriteLine("Could not find merkle records for repo2.");
                return;
            }
            CommandLineInterface.PrintLineSeparator();
            Console.WriteLine($"-------------------------------------------------");
            Console.WriteLine($"| SECOND REPO                                   |");
            Console.WriteLine($"-------------------------------------------------");
            Console.WriteLine($"");
            Console.WriteLine($"repo:   {did2}");
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
            FindMentionsForRepo3(repoFile2, did2, did1, merkle2);
            Console.WriteLine($"");


        }


        public static Dictionary<string, DagCborObject>? FindMerkleRecords(string repoFile)
        {
            if (string.IsNullOrEmpty(repoFile)) return null;

            Dictionary<string, DagCborObject> records = new Dictionary<string, DagCborObject>();

            using(var fs = new FileStream(repoFile, FileMode.Open))
            {
                // Read header
                var repoHeader = RepoHeader.ReadFromStream(fs);

                while(fs.Position < fs.Length)
                { 
                    // Read data block (record)
                    var repoRecord = RepoRecord.ReadFromStream(fs);
                    if (repoRecord == null) continue;

                    List<DagCborObject>? e = repoRecord.DataBlock.SelectObject(["e"]) as List<DagCborObject>;
                    if (e == null) continue;

                    foreach(DagCborObject node in e)
                    {
                        string? v = node.SelectString(["v"]);
                        if (string.IsNullOrEmpty(v)) continue;

                        records[v] = repoRecord.DataBlock;
                    }
                }
            }

            return records;
        }


        public static string? FindDidForRepo(string? repoFile)
        {
            if (string.IsNullOrEmpty(repoFile)) return null;

            using(var fs = new FileStream(repoFile, FileMode.Open))
            {
                // Read header
                var repoHeader = RepoHeader.ReadFromStream(fs);

                while(fs.Position < fs.Length)
                { 
                    // Read data block (record)
                    var repoRecord = RepoRecord.ReadFromStream(fs);

                    string? did = repoRecord.DataBlock.SelectString(["did"]);
                    string? rev = repoRecord.DataBlock.SelectString(["rev"]);
                    string? data = repoRecord.DataBlock.SelectString(["data"]);
                    string? version = repoRecord.DataBlock.SelectString(["version"]);

                    if (string.IsNullOrEmpty(did) == false 
                        && string.IsNullOrEmpty(rev) == false 
                        && string.IsNullOrEmpty(data) == false 
                        && string.IsNullOrEmpty(version) == false)
                    {
                        return did;
                    }
                }
            }

            return null;
        }

        public static void FindLikesForRepo(string? repoFile, string? sourceDid, string? targetDid)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid)) return;

            // read file
            using(var fs = new FileStream(repoFile, FileMode.Open))
            {
                // read header
                var repoHeader = RepoHeader.ReadFromStream(fs);

                while(fs.Position < fs.Length)
                { 
                    // read data block (record)
                    var repoRecord = RepoRecord.ReadFromStream(fs);

                    if (repoRecord.RecordType != "app.bsky.feed.like") continue;

                    string? uri = repoRecord.DataBlock.SelectString(["subject", "uri"]);
                    if (uri == null || uri.Contains(targetDid) == false) continue;

                    string clickableUri = uri.Replace("at://", "https://bsky.app/profile/").Replace("app.bsky.feed.post/", "post/");
                    Console.WriteLine($"liked -->   {clickableUri}");
                }
            }
        }


        public static void FindRepliesForRepo(string? repoFile, string? sourceDid, string? targetDid)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid)) return;

            using(var fs = new FileStream(repoFile, FileMode.Open))
            {
                // read header
                var repoHeader = RepoHeader.ReadFromStream(fs);

                while(fs.Position < fs.Length)
                { 
                    // read data block (record)
                    var repoRecord = RepoRecord.ReadFromStream(fs);

                    if (repoRecord.RecordType != "app.bsky.feed.post") continue;

                    string? uriReply = repoRecord.DataBlock.SelectString(["reply", "parent", "uri"]);
                    string? uriRoot = repoRecord.DataBlock.SelectString(["reply", "root", "uri"]);

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
                }
            }
        }


        
        public static void FindRepostsForRepo(string? repoFile, string? sourceDid, string? targetDid)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid)) return;

            using(var fs = new FileStream(repoFile, FileMode.Open))
            {
                // Read header
                var repoHeader = RepoHeader.ReadFromStream(fs);

                while(fs.Position < fs.Length)
                { 
                    // Read data block (record)
                    var repoRecord = RepoRecord.ReadFromStream(fs);
                    
                    if (repoRecord.RecordType != "app.bsky.feed.repost") continue;

                    string? uri = repoRecord.DataBlock.SelectString(["subject", "uri"]);

                    if (uri == null || uri.Contains(targetDid) == false) continue;

                    string clickableUri = uri.Replace("at://", "https://bsky.app/profile/").Replace("app.bsky.feed.post/", "post/");
                    Console.WriteLine($"reposted -->   {clickableUri}");
                }
            }
        }


        
        public static void FindQuotePostsForRepo(string? repoFile, string? sourceDid, string? targetDid)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid)) return;

            // read file
            using(var fs = new FileStream(repoFile, FileMode.Open))
            {
                // read header
                var repoHeader = RepoHeader.ReadFromStream(fs);

                while(fs.Position < fs.Length)
                { 
                    // read data block (record)
                    var repoRecord = RepoRecord.ReadFromStream(fs);

                    if (repoRecord.RecordType != "app.bsky.feed.post") continue;

                    string? uri = repoRecord.DataBlock.SelectString(["embed", "record", "uri"]);

                    if (uri == null || uri.Contains(targetDid) == false) continue;

                    string clickableUri = uri.Replace("at://", "https://bsky.app/profile/").Replace("app.bsky.feed.post/", "post/");
                    Console.WriteLine($"quote posted -->   {clickableUri}");
                }
            }
        }


        

        public static void FindMentionsForRepo3(string? repoFile, string? sourceDid, string? targetDid, Dictionary<string, DagCborObject>? merkle)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid) || merkle == null) return;

            using(var fs = new FileStream(repoFile, FileMode.Open))
            {
                // Read header
                var repoHeader = RepoHeader.ReadFromStream(fs);

                while(fs.Position < fs.Length)
                { 
                    // Read data block (record)
                    var repoRecord = RepoRecord.ReadFromStream(fs);
                    if (repoRecord.RecordType != "app.bsky.feed.post") continue;

                    string? text = repoRecord.DataBlock.SelectString(["text"]) as string;
                    if (text == null) continue;

                    List<DagCborObject>? facets = repoRecord.DataBlock.SelectObject(["facets"]) as List<DagCborObject>;
                    if (facets == null) continue;

                    foreach(DagCborObject facet in facets)
                    {
                        List<DagCborObject>? featuresArray = facet.SelectObject(["features"]) as List<DagCborObject>;
                        if (featuresArray == null) continue;
                        if (featuresArray.Count != 1) continue;

                        DagCborObject feature = featuresArray[0];
                        string? did = feature.SelectString(["did"]);
                        string? type = feature.SelectString(["$type"]);

                        if (type != "app.bsky.richtext.facet#mention" || did != targetDid) continue;

                        string? rkey = FindRkeyForCid(repoRecord.Cid.Base32, merkle);

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
                }
            }
        }


        /// <summary>
        /// 
        /// Walk the elements of the node entry and find the rkey for the given cid.
        /// 
        /// https://atproto.com/specs/repository#repo-data-structure-v3
        /// 
        /// </summary>
        /// <param name="cid"></param>
        /// <param name="merkle"></param>
        /// <returns></returns>
        public static string? FindRkeyForCid(string cid, Dictionary<string, DagCborObject>? merkle)
        {
            if (string.IsNullOrEmpty(cid) || merkle == null) return null;
            if (merkle.ContainsKey(cid) == false) return null;

            DagCborObject? record = merkle[cid];
            if (record == null) return null;

            List<DagCborObject>? e = record.SelectObject(["e"]) as List<DagCborObject>;
            if (e == null) return null;

            string? kCurrent = null;

            foreach(DagCborObject node in e)
            {
                string? v = node.SelectString(["v"]);
                if (string.IsNullOrEmpty(v)) return null;
                string? k = node.SelectString(["k"]);
                if (string.IsNullOrEmpty(v)) return null;
                int? p = node.SelectInt(["p"]);
                if (p == null) return null;

                if (p == 0)
                {
                    kCurrent = k;
                }
                else if (kCurrent != null)
                {
                    kCurrent = kCurrent.Substring(0, (int)p) + k;
                }
                else
                {
                    return null;
                }

                if (string.Equals(cid, v)) break; // we're done
            }


            if (kCurrent == null) return null;

            string rkey = kCurrent.Split("/").Last();
            return rkey;
        }
   }
}