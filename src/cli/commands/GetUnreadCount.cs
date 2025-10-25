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

            //
            // Load lfs
            //
            LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
            ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);
            SessionFile? session = lfs?.LoadSession(actorInfo);


            //
            // Call WS
            //
            string url = $"https://{session?.pds}/xrpc/app.bsky.notification.getUnreadCount";
            JsonNode? response = BlueskyClient.SendRequest(url,
                HttpMethod.Get, 
                accessJwt: session?.accessJwt);


            //
            // Print results
            //
            BlueskyClient.PrintJsonResponseToConsole(response);
        }
    }
}