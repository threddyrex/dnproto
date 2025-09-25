using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands
{
    public class DeleteActivityForMonth : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return ["dataDir", "handle", "password", "month"];
        }

        public override HashSet<string> GetOptionalArguments()
        {
            return ["authFactorToken"];
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
            string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");
            string? password = CommandLineInterface.GetArgumentValue(arguments, "password");
            string? authFactorToken = CommandLineInterface.GetArgumentValue(arguments, "authFactorToken");
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? month = CommandLineInterface.GetArgumentValue(arguments, "month");

            //
            // Log in.
            //
            JsonNode? session = BlueskyClient.CreateSession(handle, password, authFactorToken);
            if (session == null)
            {
                Logger.LogError("Failed to create session. Please log in.");
                return;
            }

            //
            // Get values from session
            //
            string? accessJwt = JsonData.SelectString(session, "accessJwt");
            string? pds = JsonData.SelectString(session, "pds");
            string? did = JsonData.SelectString(session, "did");

            Logger.LogInfo($"pds: {pds}");
            Logger.LogInfo($"did: {did}");

            if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(accessJwt) || string.IsNullOrEmpty(did))
            {
                Logger.LogError("Session not found. Please log in.");
                return;
            }


            //
            // Get bookmarks from Bsky
            //
            List<(string createdAt, AtUri uri)> bookmarks = BlueskyClient.GetBookmarks(pds, accessJwt);
            var bookmarksSorted = bookmarks.OrderBy(b => b.createdAt).ToList();
            HashSet<string> bookmarkRkeys = new HashSet<string>();
            int bookmarkCount = bookmarksSorted.Count;
            Logger.LogInfo($"bookmarks.Count: {bookmarks.Count}");
            Logger.LogInfo($"bookmarksSorted.Count: {bookmarksSorted.Count}");

            foreach ((string createdAt, AtUri b) in bookmarksSorted)
            {
                if (string.IsNullOrEmpty(b.Rkey))
                {
                    continue;
                }

                Logger.LogTrace($"BOOKMARK [{createdAt}] {b.ToBskyPostUrl()}");
                bookmarkRkeys.Add(b.Rkey);
            }


            //
            // Initialize local file system. We'll be reading the repo from here.
            //
            LocalFileSystem? localFileSystem = LocalFileSystem.Initialize(dataDir, Logger);
            if (localFileSystem == null)
            {
                Logger.LogError("Failed to initialize local file system.");
                return;
            }


            string? repoFile = localFileSystem.GetPath_RepoFile(handle);
            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError($"Repo file does not exist: {repoFile}");
                return;
            }


            //
            // Walk repo once to get cid/rkey mapping
            //
            Dictionary<string, string>? rkeys = Repo.FindRkeys(repoFile);
            Logger.LogInfo($"Repo.FindRkeys() Count: {rkeys?.Count}");


            //
            // Walk repo again for posts and likes
            //
            List<RepoRecord> records = new List<RepoRecord>();

            Repo.WalkRepo(
                repoFile,
                (repoHeader) =>
                {
                    return true;
                },
                (repoRecord) =>
                {
                    if (string.IsNullOrEmpty(repoRecord.RecordType)) return true;
                    if (string.IsNullOrEmpty(repoRecord.CreatedAt)) return true;


                    if (DateTime.TryParse(repoRecord.CreatedAt, out DateTime createdAt))
                    {
                        string monthForPost = createdAt.ToString("yyyy-MM");

                        if (string.Equals(monthForPost, month, StringComparison.OrdinalIgnoreCase) == false)
                        {
                            return true;
                        }

                        if (string.Equals(repoRecord.RecordType, "app.bsky.feed.post", StringComparison.OrdinalIgnoreCase))
                        {
                            records.Add(repoRecord);
                            return true;
                        }

                        if (string.Equals(repoRecord.RecordType, "app.bsky.feed.like", StringComparison.OrdinalIgnoreCase))
                        {
                            records.Add(repoRecord);
                            return true;
                        }
                    }


                    return true;
                }
            );


            //
            // Print, sorted
            //
            int likeCount = records.Where(r => string.Equals(r.RecordType, "app.bsky.feed.like", StringComparison.OrdinalIgnoreCase)).Count();
            Logger.LogInfo($"likeCount: {likeCount}");

            var sortedRecords = records.OrderBy(pr => pr.DataBlock.SelectString(["createdAt"]));
            foreach (var repoRecord in sortedRecords)
            {
                string? rkey = rkeys != null && repoRecord.Cid != null && rkeys.TryGetValue(repoRecord.Cid.GetBase32(), out string? foundRkey) ? foundRkey : null;

                if (string.Equals(repoRecord.RecordType, "app.bsky.feed.like", StringComparison.OrdinalIgnoreCase))
                {
                    // don't log these, just skip
                    continue;
                }
                else if (string.IsNullOrEmpty(rkey) == false)
                {
                    if (bookmarkRkeys.Contains(rkey))
                    {
                        Logger.LogInfo($"[{repoRecord.DataBlock.SelectString(["createdAt"])}] https://bsky.app/profile/{handle}/post/{rkey} (BOOKMARKED)");
                    }
                    else
                    {
                        Logger.LogInfo($"[{repoRecord.DataBlock.SelectString(["createdAt"])}] https://bsky.app/profile/{handle}/post/{rkey}");
                    }
                }
                else
                {
                    Logger.LogInfo($"[{repoRecord.DataBlock.SelectString(["createdAt"])}] {repoRecord.Cid?.GetBase32()}");
                }
            }


            //
            // Wait for user to type "yes" before continuing
            //
            Logger.LogInfo("Please review the printed posts. Anything not marked (BOOKMARKED) will be deleted. Type 'yes' to continue...");
            string? userInput = Console.ReadLine();
            if (string.Equals(userInput, "yes", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInfo("Continuing...");
            }
            else
            {
                Logger.LogInfo("exiting...");
            }


            //
            // To be continued...
            //

        }
    }
}