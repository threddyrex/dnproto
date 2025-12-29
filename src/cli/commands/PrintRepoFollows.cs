using System.Text;

using dnproto.fs;
using dnproto.repo;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands
{
    public class PrintRepoFollows : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"actor"});
        }

        public override HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>(new string[]{"month"});
        }

        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");
            string? month = CommandLineInterface.GetArgumentValue(arguments, "month");

            //
            // Load lfs
            //
            LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
            ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

            //
            // Get local path of repo file (assumes user called GetRepo first to pull it down).
            //
            string? repoFile = lfs?.GetPath_RepoFile(actorInfo);
            if (string.IsNullOrEmpty(repoFile) || File.Exists(repoFile) == false)
            {
                Logger.LogError($"Repo file does not exist: {repoFile}");
                return;
            }

            //
            // Walk repo
            //
            List<RepoRecord> follows = new List<RepoRecord>();

            Repo.WalkRepo(
                repoFile,
                (repoHeader) =>
                {
                    return true;
                },
                (repoRecord) =>
                {
                    if (string.IsNullOrEmpty(repoRecord.AtProtoType)) return true;
                    if (string.Equals(repoRecord.AtProtoType, "app.bsky.graph.follow", StringComparison.OrdinalIgnoreCase) == false) return true;


                    if (string.IsNullOrEmpty(month) == false)
                    {
                        if (DateTime.TryParse(repoRecord.CreatedAt, out DateTime createdAt))
                        {
                            string postMonth = createdAt.ToString("yyyy-MM");
                            if (month.Equals(postMonth))
                            {
                                follows.Add(repoRecord);
                            }
                        }
                    }
                    else
                    {
                        follows.Add(repoRecord);
                    }

                    return true;
                }
            );


            //
            // Print, sorted
            //
            var sortedFollows = follows.OrderBy(fr => fr.DataBlock.SelectString(["createdAt"]));
            foreach (var repoRecord in sortedFollows)
            {
                Logger.LogInfo($"[{repoRecord.DataBlock.SelectString(["createdAt"])}] https://bsky.app/profile/{repoRecord.DataBlock.SelectString(["subject"])}");

                //
                // Print text content
                //
                string? text = repoRecord.JsonString;
                Logger.LogTrace(text);
            }

            Logger.LogInfo($"Total follows found: {follows.Count}");
        }
   }
}