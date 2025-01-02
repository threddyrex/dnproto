using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.helpers;

namespace dnproto.commands
{
    public class GetUnreadCount : ICommand
    {
        public HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>();
        }

        public HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>();
        }


        /// <summary>
        /// Get unread notification count
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public void DoCommand(Dictionary<string, string> arguments)
        {
            string accessJwt = LocalStateHelpers.ReadSessionProperty("sessionAccessJwt");
            string pds = LocalStateHelpers.ReadSessionProperty("sessionPds");
            string did = LocalStateHelpers.ReadSessionProperty("sessionDid");

            Console.WriteLine("did: " + did);

            JsonNode? unreadCount = DoGetUnreadCount(pds, accessJwt);

            if(unreadCount == null)
            {
                Console.WriteLine("GetUnreadCount returned null.");
                return;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine("Unread count:");
            Console.WriteLine(unreadCount.ToJsonString(options));
        }

        
        /// <summary>
        /// Get unread notification count
        /// </summary>
        /// <param name="pds"></param>
        /// <param name="accessJwt"></param>
        /// <returns></returns>
        public static JsonNode? DoGetUnreadCount(string pds, string accessJwt)
        {
            using (HttpClient client = new HttpClient())
            {
                // Setup request
                string url = $"https://{pds}/xrpc/app.bsky.notification.getUnreadCount";
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);;
                

                // Send
                var response = client.Send(request);

                using (var reader = new StreamReader(response.Content.ReadAsStream()))
                {
                    var responseText = reader.ReadToEnd();
                    var ret = JsonNode.Parse(responseText);
                    return ret;
                }
            }
        }
    }
}