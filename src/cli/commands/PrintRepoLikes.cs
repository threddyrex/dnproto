using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using dnproto.repo;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands
{
    public class PrintRepoLikes : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"dataDir", "handle"});
        }

        public override HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>(new string[]{"month"});
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
            string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");
            string? month = CommandLineInterface.GetArgumentValue(arguments, "month");

            //
            // Get local path of repo file (assumes user called GetRepo first to pull it down).
            //
            string? repoFile = LocalFileSystem.Initialize(dataDir, Logger)?.GetPath_RepoFile(handle);
            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError($"Repo file does not exist: {repoFile}");
                return;
            }


            //
            // Walk repo
            //
            List<RepoRecord> likes = new List<RepoRecord>();

            Repo.WalkRepo(
                repoFile,
                (repoHeader) => { return true; },
                (repoRecord) =>
                {
                    if (string.Equals(repoRecord.RecordType, "app.bsky.feed.like") == false) return true;
                    if (string.IsNullOrEmpty(repoRecord.CreatedAt)) return true;

 
                    if (string.IsNullOrEmpty(month) == false)
                    {
                        if (DateTime.TryParse(repoRecord.CreatedAt, out DateTime createdAt))
                        {
                            string postMonth = createdAt.ToString("yyyy-MM");
                            if (month.Equals(postMonth))
                            {
                                likes.Add(repoRecord);
                            }
                        }
                    }
                    else
                    {
                        likes.Add(repoRecord);
                    }

                    return true;
                }
            );





            //
            // Print, sorted
            //
            var sortedLikes = likes.OrderBy(pr => pr.DataBlock.SelectString(["createdAt"]));
            foreach (var repoRecord in sortedLikes)
            {
                string? uri = repoRecord.DataBlock.SelectString(["subject", "uri"]);
                if (uri == null) continue;

                string? bskyUrl = AtUri.FromAtUri(uri)?.ToBskyPostUrl();

                Logger.LogInfo($"[{repoRecord.DataBlock.SelectString(["createdAt"])}] {bskyUrl}");
            }
        }
   }
}