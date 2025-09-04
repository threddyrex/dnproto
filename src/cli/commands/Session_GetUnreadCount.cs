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
    public class Session_GetUnreadCount : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"sessionFile"});
        }

        public override HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>(new string[]{"pds", "outfile"});
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
            // Find existing session on disk
            //
            JsonNode? session = JsonData.ReadJsonFromFile(CommandLineInterface.GetArgumentValue(arguments, "sessionFile"));

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

            string url = $"https://{pds}/xrpc/app.bsky.notification.getUnreadCount";
            Logger.LogInfo($"url: {url}");

            //
            // Call WS
            //
            JsonNode? response = BlueskyClient.SendRequest(url,
                HttpMethod.Get, 
                accessJwt: accessJwt);


            //
            // Print results
            //
            BlueskyClient.PrintJsonResponseToConsole(response);
            JsonData.WriteJsonToFile(response, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
        }
    }
}