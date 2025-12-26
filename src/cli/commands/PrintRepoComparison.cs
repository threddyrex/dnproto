using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using dnproto.fs;
using dnproto.repo;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands
{
    public class PrintRepoComparison : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"actor1", "actor2"});
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
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? actor1 = CommandLineInterface.GetArgumentValue(arguments, "actor1");
            string? actor2 = CommandLineInterface.GetArgumentValue(arguments, "actor2");


            //
            // Get local files
            //
            var lfs = LocalFileSystem.Initialize(dataDir, Logger);
            var actorInfo1 = lfs?.ResolveActorInfo(actor1);
            var actorInfo2 = lfs?.ResolveActorInfo(actor2);
            var repoFile1 = lfs?.GetPath_RepoFile(actorInfo1);
            var repoFile2 = lfs?.GetPath_RepoFile(actorInfo2);

            Logger.LogInfo($"repoFile1: {repoFile1}");
            Logger.LogInfo($"repoFile2: {repoFile2}");

            if (string.IsNullOrEmpty(repoFile1) || string.IsNullOrEmpty(repoFile2) || File.Exists(repoFile1) == false || File.Exists(repoFile2) == false)
            {
                Logger.LogError("Could not find the repo files.");
                return;
            }



            //
            // Find did for repos
            //
            string? did1 = RepoUtils.FindDid(repoFile1);
            string? did2 = RepoUtils.FindDid(repoFile2);

            Logger.LogInfo($"did1: {did1}");
            Logger.LogInfo($"did2: {did2}");

            if(string.IsNullOrEmpty(did1) || string.IsNullOrEmpty(did2))
            {
                Logger.LogError("Did not find did for repo1 or repo2.");
                return;
            }


            //
            // repo1 --> did2
            //
            Dictionary<string, string>? rkeys1 = RepoUtils.FindRkeys(repoFile1);
            if (rkeys1 == null)
            {
                Logger.LogError("Could not find merkle records for repo1.");
                return;
            }
            Logger.LogInfo($"-------------------------------------------------");
            Logger.LogInfo($"| FIRST REPO                                    |");
            Logger.LogInfo($"-------------------------------------------------");
            Logger.LogInfo($"");
            Logger.LogInfo($"USER:   {did1}");
            Logger.LogInfo($"");
            Logger.LogInfo($"");
            FindLikesForRepo(repoFile1, did1, did2);
            Logger.LogInfo($"");
            FindRepliesForRepo(repoFile1, did1, did2);
            Logger.LogInfo($"");
            FindRepostsForRepo(repoFile1, did1, did2);
            Logger.LogInfo($"");
            FindQuotePostsForRepo(repoFile1, did1, did2);
            Logger.LogInfo($"");
            FindMentionsForRepo3(repoFile1, did1, did2, rkeys1);
            Logger.LogInfo($"");


            //
            // repo2 --> did1
            //
            Dictionary<string, string>? rkeys2 = RepoUtils.FindRkeys(repoFile2);
            if (rkeys2 == null)
            {
                Logger.LogError("Could not find merkle records for repo2.");
                return;
            }
            Logger.LogInfo($"-------------------------------------------------");
            Logger.LogInfo($"| SECOND REPO                                   |");
            Logger.LogInfo($"-------------------------------------------------");
            Logger.LogInfo($"");
            Logger.LogInfo($"USER:   {did2}");
            Logger.LogInfo($"");
            Logger.LogInfo($"");
            FindLikesForRepo(repoFile2, did2, did1);
            Logger.LogInfo($"");
            FindRepliesForRepo(repoFile2, did2, did1);
            Logger.LogInfo($"");
            FindRepostsForRepo(repoFile2, did2, did1);
            Logger.LogInfo($"");
            FindQuotePostsForRepo(repoFile2, did2, did1);
            Logger.LogInfo($"");
            FindMentionsForRepo3(repoFile2, did2, did1, rkeys2);
            Logger.LogInfo($"");

        }



        public void FindLikesForRepo(string? repoFile, string? sourceDid, string? targetDid)
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
                    Logger.LogInfo($"liked -->   {clickableUri}");

                    return true;
                }
            );
        }


        public void FindRepliesForRepo(string? repoFile, string? sourceDid, string? targetDid)
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
                        Logger.LogInfo($"replied to -->   {clickableUri}");
                    }
                    else if (uriRoot != null && uriRoot.Contains(targetDid))
                    {
                        string clickableUri = uriRoot.Replace("at://", "https://bsky.app/profile/").Replace("app.bsky.feed.post/", "post/");
                        Logger.LogInfo($"replied to -->   {clickableUri}");
                    }

                    return true;
                }
            );
        }


        
        public void FindRepostsForRepo(string? repoFile, string? sourceDid, string? targetDid)
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
                    Logger.LogInfo($"reposted -->   {clickableUri}");

                    return true;
                }
            );
        }


        
        public void FindQuotePostsForRepo(string? repoFile, string? sourceDid, string? targetDid)
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
                    Logger.LogInfo($"quote posted -->   {clickableUri}");

                    return true;
                }
            );
        }


        

        public void FindMentionsForRepo3(string? repoFile, string? sourceDid, string? targetDid, Dictionary<string, string>? rkeys)
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
                            Logger.LogInfo($"------------------------");
                            Logger.LogInfo($"| mentioned (no rkey)  |");
                            Logger.LogInfo($"------------------------");
                            Logger.LogInfo($"{text}");
                            Logger.LogInfo("");
                        }
                        else
                        {
                            string clickableUri = $"https://bsky.app/profile/{sourceDid}/post/{rkey}";
                            Logger.LogInfo($"mentioned -->   {clickableUri}");
                        }
                    }

                    return true;
                }
            );
        }
   }
}