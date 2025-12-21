using System.Text;

using dnproto.sdk.repo;
using dnproto.sdk.ws;
using dnproto.sdk.fs;
using dnproto.sdk.mst;

namespace dnproto.cli.commands
{
    public class PrintRepoMstAuthorFeed : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"actor"});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");

            //
            // Load lfs
            //
            LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
            ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

            //
            // Get local repo file
            //
            string? repoFile = LocalFileSystem.Initialize(dataDir, Logger)?.GetPath_RepoFile(actorInfo);
            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError($"Repo file does not exist: {repoFile}");
                return;
            }


            //
            // Load mst
            //
            MstRepository? mstRepo = MstRepository.LoadFromFile(repoFile, Logger);
            if (mstRepo == null)
            {
                Logger.LogError("Failed to load MST repository");
                return;
            }

            Logger.LogInfo("Loading author feed...");
            
            // Get all records and show collection summary
            var allRecords = mstRepo.ListRecords();
            Logger.LogInfo($"Total records in repository: {allRecords.Count}");
            
            // Group by collection
            var collections = allRecords
                .Select(key => key.Contains('/') ? key.Substring(0, key.IndexOf('/')) : key)
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .ToList();
            
            Logger.LogInfo("Collections in repository:");
            foreach (var group in collections)
            {
                Logger.LogInfo($"  {group.Key}: {group.Count()} records");
            }
            
            Logger.LogInfo("");
            
            var feed = AuthorFeed.GetAuthorFeed(mstRepo);

            if (feed.Count == 0)
            {
                Logger.LogInfo("No posts or reposts found in this repository.");
                return;
            }

            Logger.LogInfo($"Found {feed.Count} feed items:\n");

            foreach (var item in feed)
            {
                if (item.RecordType == "app.bsky.feed.post" && !string.IsNullOrEmpty(item.Text))
                {
                    Logger.LogInfo($"[{item.CreatedAt}] POST: {item.Text}");
                }
                else if (item.RecordType == "app.bsky.feed.repost" && !string.IsNullOrEmpty(item.SubjectUri))
                {
                    Logger.LogInfo($"[{item.CreatedAt}] REPOST: {item.SubjectUri}");
                }
                else
                {
                    Logger.LogInfo($"[{item.CreatedAt}] {item.RecordType}: {item.Path}");
                }
            }
        }

   }
}