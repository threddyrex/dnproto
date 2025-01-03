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

            Console.WriteLine($"pds: {pds}");
            Console.WriteLine($"did: {did}");

            if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(accessJwt) || string.IsNullOrEmpty(did))
            {
                Console.WriteLine("Session not found. Please log in.");
                return;
            }


            //
            // Call WS
            //
            JsonNode? unreadCount = WebServiceClient.SendRequest($"https://{pds}/xrpc/app.bsky.notification.getUnreadCount",
                HttpMethod.Get, 
                accessJwt: accessJwt);


            //
            // Print results
            //
            if(unreadCount == null)
            {
                Console.WriteLine("GetUnreadCount returned null.");
                return;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine("response:");
            Console.WriteLine(unreadCount.ToJsonString(options));
        }
    }
}