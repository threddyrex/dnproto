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
        /// Delete session
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

            Console.WriteLine($"pds: {pds}");
            Console.WriteLine($"did: {did}");

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

            JsonNode? response = WebServiceClient.SendRequest($"https://{pds}/xrpc/com.atproto.server.deleteSession",
                HttpMethod.Post, 
                accessJwt: refreshJwt);


            //
            // Print results
            //
            WebServiceClient.PrintJsonResponseToConsole(response);
        }
    }
}