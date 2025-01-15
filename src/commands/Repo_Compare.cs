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
            string? repo1Did = FindDidForRepo(repoFile1);
            string? repo2Did = FindDidForRepo(repoFile2);

            Console.WriteLine($"repo1Did: {repo1Did}");
            Console.WriteLine($"repo2Did: {repo2Did}");
            Console.WriteLine($"");

            if(string.IsNullOrEmpty(repo1Did) || string.IsNullOrEmpty(repo2Did))
            {
                Console.WriteLine("Did not find did for repo1 or repo2.");
                return;
            }


            //
            // repo1 likes repo2
            //
            CommandLineInterface.PrintLineSeparator();
            Console.WriteLine($"repo1: {repo1Did}");
            Console.WriteLine($"");
            FindLikesForRepo(repoFile1, repo1Did, repo2Did);
            Console.WriteLine($"");
            FindRepliesForRepo(repoFile1, repo1Did, repo2Did);
            Console.WriteLine($"");
            FindRepostsForRepo(repoFile1, repo1Did, repo2Did);
            Console.WriteLine($"");
            FindQuotePostsForRepo(repoFile1, repo1Did, repo2Did);
            Console.WriteLine($"");
            FindMentionsForRepo(repoFile1, repo1Did, repo2Did);
            Console.WriteLine($"");

            CommandLineInterface.PrintLineSeparator();
            Console.WriteLine($"repo2: {repo2Did}");
            Console.WriteLine($"");
            FindLikesForRepo(repoFile2, repo2Did, repo1Did);
            Console.WriteLine($"");
            FindRepliesForRepo(repoFile2, repo2Did, repo1Did);
            Console.WriteLine($"");
            FindRepostsForRepo(repoFile2, repo2Did, repo1Did);
            Console.WriteLine($"");
            FindQuotePostsForRepo(repoFile2, repo2Did, repo1Did);
            Console.WriteLine($"");
            FindMentionsForRepo(repoFile2, repo2Did, repo1Did);
            Console.WriteLine($"");


        }

        public static string? FindDidForRepo(string? repoFile)
        {
            if (string.IsNullOrEmpty(repoFile))
            {
                return null;
            }

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

                    string? did = repoRecord.Record.GetMapValueAtPath(["did"]);
                    string? rev = repoRecord.Record.GetMapValueAtPath(["rev"]);
                    string? data = repoRecord.Record.GetMapValueAtPath(["data"]);
                    string? version = repoRecord.Record.GetMapValueAtPath(["version"]);

                    if (string.IsNullOrEmpty(did) == false && string.IsNullOrEmpty(rev) == false && string.IsNullOrEmpty(data) == false && string.IsNullOrEmpty(version) == false)
                    {
                        return did;
                    }
                }
            }

            return null;
        }

        public static void FindLikesForRepo(string? repoFile, string? sourceDid, string? targetDid)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid))
            {
                return;
            }

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

                    if (repoRecord.RecordType == "app.bsky.feed.like")
                    {
                        string? uri = repoRecord.Record.GetMapValueAtPath(["subject", "uri"]);

                        if (uri != null && uri.Contains(targetDid))
                        {
                            string clickableUri = uri.Replace("at://", "https://bsky.app/profile/").Replace("app.bsky.feed.post/", "post/");
                            Console.WriteLine($"liked -->   {clickableUri}");
                        }
                    }
                }
            }
        }

        public static void FindRepliesForRepo(string? repoFile, string? sourceDid, string? targetDid)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid))
            {
                return;
            }

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

                    if (repoRecord.RecordType == "app.bsky.feed.post")
                    {
                        string? uriReply = repoRecord.Record.GetMapValueAtPath(["reply", "parent", "uri"]);
                        string? uriRoot = repoRecord.Record.GetMapValueAtPath(["reply", "root", "uri"]);

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
        }


        
        public static void FindRepostsForRepo(string? repoFile, string? sourceDid, string? targetDid)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid))
            {
                return;
            }

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

                    if (repoRecord.RecordType == "app.bsky.feed.repost")
                    {
                        string? uri = repoRecord.Record.GetMapValueAtPath(["subject", "uri"]);

                        if (uri != null && uri.Contains(targetDid))
                        {
                            string clickableUri = uri.Replace("at://", "https://bsky.app/profile/").Replace("app.bsky.feed.post/", "post/");
                            Console.WriteLine($"reposted -->   {clickableUri}");
                        }
                    }
                }
            }
        }


        
        public static void FindQuotePostsForRepo(string? repoFile, string? sourceDid, string? targetDid)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid))
            {
                return;
            }

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

                    if (repoRecord.RecordType == "app.bsky.feed.post")
                    {
                        string? uri = repoRecord.Record.GetMapValueAtPath(["embed", "record", "uri"]);

                        if (uri != null && uri.Contains(targetDid))
                        {
                            string clickableUri = uri.Replace("at://", "https://bsky.app/profile/").Replace("app.bsky.feed.post/", "post/");
                            Console.WriteLine($"quote posted -->   {clickableUri}");
                        }
                    }
                }
            }
        }


        
        public static void FindMentionsForRepo(string? repoFile, string? sourceDid, string? targetDid)
        {
            if (string.IsNullOrEmpty(repoFile) || string.IsNullOrEmpty(sourceDid) || string.IsNullOrEmpty(targetDid))
            {
                return;
            }

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

                    if (repoRecord.RecordType == "app.bsky.feed.post")
                    {
                        Dictionary<string, object>? recordRaw = (Dictionary<string, object>?) repoRecord.Record.GetRawValue();

                        if (recordRaw == null) continue;
                        if (recordRaw.ContainsKey("text") == false) continue;
                        if (recordRaw.ContainsKey("facets") == false) continue;

                        string? text = recordRaw["text"] as string;

                        List<object>? facets = recordRaw["facets"] as List<object>;

                        if(facets == null) continue;

                        foreach(Dictionary<string, object>? facet in facets)
                        {
                            if(facet == null) continue;
                            if (facet.ContainsKey("features") == false) continue;

                            List<object>? features = facet["features"] as List<object>;

                            if(features == null) continue;

                            foreach(Dictionary<string, object> feature in features)
                            {
                                if (feature.ContainsKey("did") == false) continue;
                                if (feature.ContainsKey("$type") == false) continue;

                                string? did = feature["did"] as string;
                                string? type = feature["$type"] as string;

                                if (type == "app.bsky.richtext.facet#mention" && did == targetDid)
                                {
                                    Console.WriteLine($"------------------");
                                    Console.WriteLine($"| mentioned -->  |");
                                    Console.WriteLine($"------------------");
                                    Console.WriteLine($"{text}");
                                    Console.WriteLine("");
                                }
                            }
                        }
                    }
                }
            }
        }

   }
}