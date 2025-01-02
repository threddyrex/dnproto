using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.helpers;

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
            string accessJwt = LocalStateHelpers.ReadSessionProperty("sessionAccessJwt");
            string pds = LocalStateHelpers.ReadSessionProperty("sessionPds");
            string did = LocalStateHelpers.ReadSessionProperty("sessionDid");
            string text = arguments["text"];

            if(string.IsNullOrEmpty(text))
            {
                Console.WriteLine("Text is required.");
                return;
            }

            Console.WriteLine("did: " + did);

            JsonNode? postResult = DoCreatePost(pds, did, accessJwt, text);

            if(postResult == null)
            {
                Console.WriteLine("CreatePost returned null.");
                return;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine("Post result:");
            Console.WriteLine(postResult.ToJsonString(options));
        }

        
        /// <summary>
        /// Create post
        /// </summary>
        /// <param name="pds"></param>
        /// <param name="did"></param>
        /// <param name="accessJwt"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public static JsonNode? DoCreatePost(string pds, string did, string accessJwt, string? text)
        {
            using (HttpClient client = new HttpClient())
            {
                // Setup request
                string url = $"https://{pds}/xrpc/com.atproto.repo.createRecord";
                var request = new HttpRequestMessage(HttpMethod.Post, url);

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);;

                request.Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        repo = did,
                        collection = "app.bsky.feed.post",
                        record = new {
                            text = text,
                            createdAt = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                        }                    
                    }));

                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                

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