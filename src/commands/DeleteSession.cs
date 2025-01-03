using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands
{
    public class DeleteSession : BaseCommand
    {
        /// <summary>
        /// Delete session. At the moment, you can't call this with the accessJwt - that
        /// one needs to expire on the server on its own. But you can delete the refreshJwt.
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string pds = LocalStateSession.ReadSessionProperty("pds");
            string did = LocalStateSession.ReadSessionProperty("did");
            string refreshJwt = LocalStateSession.ReadSessionProperty("refreshJwt");
            string url = $"https://{pds}/xrpc/com.atproto.server.deleteSession";

            Console.WriteLine($"pds: {pds}");
            Console.WriteLine($"did: {did}");
            Console.WriteLine($"url: {url}");

            //
            // Clear local state
            //
            LocalStateSession.WriteSessionProperties(new Dictionary<string, string>
            {
                {"did", ""},
                {"pds", ""},
                {"accessJwt", ""},
                {"refreshJwt", ""}
            });


            //
            // Call the API.
            // (You can't delete accessJwt, but you can delete refreshJwt.)
            //
            if (string.IsNullOrEmpty(refreshJwt))
            {
                Console.WriteLine("Session not found. Nothing to delete.");
                return;
            }

            JsonNode? response = WebServiceClient.SendRequest(url,
                HttpMethod.Post, 
                accessJwt: refreshJwt);


            //
            // Print results
            //
            WebServiceClient.PrintJsonResponseToConsole(response);
        }
    }
}