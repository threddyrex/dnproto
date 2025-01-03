using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands
{
    public class CreatePost : ICommand
    {
        public HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"text"});
        }

        public HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>(new string[]{});
        }


        /// <summary>
        /// Create post
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string accessJwt = LocalStateSession.ReadSessionProperty("accessJwt");
            string pds = LocalStateSession.ReadSessionProperty("pds");
            string did = LocalStateSession.ReadSessionProperty("did");
            string text = arguments["text"];

            Console.WriteLine($"pds: {pds}");
            Console.WriteLine($"did: {did}");
            Console.WriteLine($"text: {text}");

            if(string.IsNullOrEmpty(text))
            {
                Console.WriteLine("Text is required.");
                return;
            }


            //
            // Send request
            //
            JsonNode? postResult = WebServiceClient.SendRequest($"https://{pds}/xrpc/com.atproto.repo.createRecord",
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
            if(postResult == null)
            {
                Console.WriteLine("CreatePost returned null.");
                return;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine("response:");
            Console.WriteLine(postResult.ToJsonString(options));
        }
    }
}