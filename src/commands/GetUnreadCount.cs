using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands
{
    public class GetUnreadCount : BaseCommand
    {
        /// <summary>
        /// Get unread notification count
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Find existing session on disk
            //
            string accessJwt = LocalStateSession.ReadSessionProperty("accessJwt");
            string pds = LocalStateSession.ReadSessionProperty("pds");
            string did = LocalStateSession.ReadSessionProperty("did");
            string url = $"https://{pds}/xrpc/app.bsky.notification.getUnreadCount";

            Console.WriteLine($"pds: {pds}");
            Console.WriteLine($"did: {did}");
            Console.WriteLine($"url: {url}");

            if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(accessJwt) || string.IsNullOrEmpty(did))
            {
                Console.WriteLine("Session not found. Please log in.");
                return;
            }


            //
            // Call WS
            //
            JsonNode? response = WebServiceClient.SendRequest(url,
                HttpMethod.Get, 
                accessJwt: accessJwt);


            //
            // Print results
            //
            WebServiceClient.PrintJsonResponseToConsole(response);
        }
    }
}