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
    public class GetBookmarks : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return ["dataDir", "actor"];
        }


        /// <summary>
        /// Get bookmarks for the current session.
        /// Contains things like muted words, saved feeds, etc. 
        /// (no docs for it yet - just released today)
        /// 
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");

            // resolve handle
            var handleInfo = BlueskyClient.ResolveHandleInfo(actor);

            //
            // Load session
            //
            SessionFile? session = LocalFileSystem.Initialize(dataDir, Logger)?.LoadSession(handleInfo);
            if (session == null)
            {
                Logger.LogError($"Failed to load session for handle: {handleInfo.Did}");
                return;
            }


            //
            // Call WS
            //
            List<(string createdAt, AtUri uri)> bookmarks = BlueskyClient.GetBookmarks(session.pds, session.accessJwt);
            var bookmarksSorted = bookmarks.OrderBy(b => b.createdAt).ToList();
            Logger.LogInfo($"bookmarks.Count {bookmarks.Count}");
            Logger.LogInfo($"bookmarksSorted.Count {bookmarksSorted.Count}");

            //
            // Print results
            //
            foreach ((string createdAt, AtUri b) in bookmarksSorted)
            {
                Logger.LogInfo($"[{createdAt}] {b.ToBskyPostUrl()}");
            }

        }
    }
}