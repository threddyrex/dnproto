using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands
{
    public class CreatePost : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"text"});
        }

        /// <summary>
        /// Create post
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string accessJwt = LocalStateSession.ReadSessionProperty("accessJwt");
            string pds = LocalStateSession.ReadSessionProperty("pds");
            string did = LocalStateSession.ReadSessionProperty("did");
            string text = arguments["text"];
            string url = $"https://{pds}/xrpc/com.atproto.repo.createRecord";

            Console.WriteLine($"pds: {pds}");
            Console.WriteLine($"did: {did}");
            Console.WriteLine($"url: {url}");
            Console.WriteLine($"text: {text}");

            if(string.IsNullOrEmpty(text))
            {
                Console.WriteLine("Text is required.");
                return;
            }


            //
            // Send request
            //
            JsonNode? postResult = WebServiceClient.SendRequest(url,
                HttpMethod.Post,
                accessJwt: accessJwt,
                content: new StringContent(JsonSerializer.Serialize(new
                    {
                        repo = did,
                        collection = "app.bsky.feed.post",
                        record = new {
                            text = text,
                            createdAt = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                        }                    
                    }))
            );



            //
            // Show result
            //
            WebServiceClient.PrintJsonResponseToConsole(postResult);
        }
    }
}