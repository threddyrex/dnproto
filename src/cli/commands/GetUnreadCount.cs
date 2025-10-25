using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands
{
    public class GetUnreadCount : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"dataDir", "actor"});
        }


        /// <summary>
        /// Get unread notification count.
        /// https://docs.bsky.app/docs/api/app-bsky-notification-get-unread-count
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
            LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
            SessionFile? session = lfs?.LoadSession(handleInfo);
            if (session == null)
            {
                Logger.LogError($"Failed to load session for handle: {handleInfo.Handle}");
                return;
            }


            //
            // Call WS
            //
            string url = $"https://{session.pds}/xrpc/app.bsky.notification.getUnreadCount";
            JsonNode? response = BlueskyClient.SendRequest(url,
                HttpMethod.Get, 
                accessJwt: session.accessJwt);


            //
            // Print results
            //
            BlueskyClient.PrintJsonResponseToConsole(response);
        }
    }
}