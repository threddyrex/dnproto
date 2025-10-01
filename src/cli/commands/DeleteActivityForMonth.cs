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
            return ["authFactorToken", "deleteLikes", "deleteReposts", "deletePosts", "sleepSeconds"];
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
            bool deleteLikes = CommandLineInterface.GetArgumentValueWithDefault(arguments, "deleteLikes", false);
            bool deleteReposts = CommandLineInterface.GetArgumentValueWithDefault(arguments, "deleteReposts", false);
            bool deletePosts = CommandLineInterface.GetArgumentValueWithDefault(arguments, "deletePosts", false);
            int sleepSeconds = CommandLineInterface.GetArgumentValueWithDefault(arguments, "sleepSeconds", 2);


            //
            // Log in.
            //
            JsonNode? session = BlueskyClient.CreateSession(handle, password, authFactorToken);
            string? accessJwt = JsonData.SelectString(session, "accessJwt");
            string? pds = JsonData.SelectString(session, "pds");
            string? did = JsonData.SelectString(session, "did");

            if (session == null || string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(accessJwt) || string.IsNullOrEmpty(did) || did.StartsWith("did:") == false)
            {
                Logger.LogError("Failed to create session. Please log in.");
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
            string? repoFile = localFileSystem?.GetPath_RepoFile(handle);
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
            // Walk repo again for posts, reposts, and likes in the specified month.
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

                        if (string.Equals(repoRecord.RecordType, "app.bsky.feed.post")
                            || string.Equals(repoRecord.RecordType, "app.bsky.feed.like")
                            || string.Equals(repoRecord.RecordType, "app.bsky.feed.repost")
                            || string.Equals(repoRecord.RecordType, "blue.flashes.feed.post")
                            )
                        {
                            records.Add(repoRecord);
                            return true;
                        }
                    }


                    return true;
                }
            );

            var sortedRecords = records.OrderBy(pr => pr.DataBlock.SelectString(["createdAt"]));


            //
            // Loop through records and make determinations.
            //
            List<(RepoRecord record, string determination, bool delete)> recordsWithDeterminations = new List<(RepoRecord record, string determination, bool delete)>();

            foreach (var repoRecord in sortedRecords)
            {
                string? rkey = rkeys != null && repoRecord.Cid != null && rkeys.TryGetValue(repoRecord.Cid.GetBase32(), out string? foundRkey) ? foundRkey : null;

                if (string.IsNullOrEmpty(rkey))
                {
                    recordsWithDeterminations.Add((repoRecord, "no rkey", false));
                    continue;
                }

                if (string.Equals(repoRecord.RecordType, "app.bsky.feed.like"))
                {
                    recordsWithDeterminations.Add((repoRecord, $"deleteLikes:{deleteLikes}", deleteLikes));                    
                    continue;
                }
                else if (string.Equals(repoRecord.RecordType, "app.bsky.feed.repost"))
                {
                    recordsWithDeterminations.Add((repoRecord, $"deleteReposts:{deleteReposts}", deleteReposts));
                    continue;
                }
                else if (string.Equals(repoRecord.RecordType, "app.bsky.feed.post") || string.Equals(repoRecord.RecordType, "blue.flashes.feed.post"))
                {
                    if (bookmarkRkeys.Contains(rkey))
                    {
                        recordsWithDeterminations.Add((repoRecord, "bookmarked!", false));
                        continue;
                    }
                    else
                    {
                        recordsWithDeterminations.Add((repoRecord, $"deletePosts:{deletePosts}", deletePosts));
                        continue;
                    }
                }
                else
                {
                    recordsWithDeterminations.Add((repoRecord, "unknown collection", false));
                }
            }


            //
            // Print determinations
            //
            foreach (var (record, determination, delete) in recordsWithDeterminations)
            {
                string? rkey = rkeys != null && record.Cid != null && rkeys.TryGetValue(record.Cid.GetBase32(), out string? foundRkey) ? foundRkey : null;
                if (rkey == null)
                {
                    continue;
                }
                
                Logger.LogInfo($"[{record.CreatedAt}] [{record.RecordType}] {determination} {(delete ? "<----------- DELETE!" : "")}");

            }

            //
            // Summarize determinations
            //
            Dictionary<string, int> deleteCountsForCollection = new Dictionary<string, int>();
            Dictionary<string, int> keepCountsForCollection = new Dictionary<string, int>();

            foreach (var (record, determination, delete) in recordsWithDeterminations)
            {
                if (delete && string.IsNullOrEmpty(record.RecordType) == false)
                {
                    if (!deleteCountsForCollection.ContainsKey(record.RecordType))
                    {
                        deleteCountsForCollection[record.RecordType] = 0;
                    }
                    deleteCountsForCollection[record.RecordType]++;
                }
                else if (string.IsNullOrEmpty(record.RecordType) == false)
                {
                    if (!keepCountsForCollection.ContainsKey(record.RecordType))
                    {
                        keepCountsForCollection[record.RecordType] = 0;
                    }
                    keepCountsForCollection[record.RecordType]++;
                }
            }

            Logger.LogInfo("");
            Logger.LogInfo("Summary of records to be DELETED:");
            foreach (var kvp in deleteCountsForCollection)
            {
                Logger.LogInfo($"[{kvp.Key}]: deleting {kvp.Value} items");
            }
            Logger.LogInfo("");
            Logger.LogInfo("Summary of records to be KEPT:");
            foreach (var kvp in keepCountsForCollection)
            {
                Logger.LogInfo($"[{kvp.Key}]: keeping {kvp.Value} items");
            }
            Logger.LogInfo("");

            //
            // Wait for user to type "yes" before continuing
            //
            Logger.LogInfo("Please review the printed records. Anything marked for deletion will be deleted. Type 'yes' to continue...");
            string? userInput = Console.ReadLine();
            if (string.Equals(userInput, "yes", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInfo("Continuing...");
            }
            else
            {
                Logger.LogInfo("exiting...");
                return;
            }


            //
            // Loop again, this time doing actual deletes.
            //
            foreach (var (record, determination, delete) in recordsWithDeterminations)
            {
                string? rkey = rkeys != null && record.Cid != null && rkeys.TryGetValue(record.Cid.GetBase32(), out string? foundRkey) ? foundRkey : null;

                Logger.LogInfo($"[{record.CreatedAt}] [{record.RecordType}] {determination} {(delete ? "<----------- DELETE!" : "")}");

                if (delete)
                {
                    BlueskyClient.DeleteRecord(pds, did, accessJwt, rkey, collection: record.RecordType);
                    Thread.Sleep(sleepSeconds * 1000);
                }
            }
        }
    }
}