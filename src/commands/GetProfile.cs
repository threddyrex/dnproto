using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace dnproto.commands
{
    public class GetProfile : ICommand
    {
        public HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"actor"});
        }

        public HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>();
        }


        /// <summary>
        /// Gets user profile.
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public void DoCommand(Dictionary<string, string> arguments)
        {
            Console.WriteLine("Getting profile...");

            JsonNode? profile = DoGetProfile(arguments["actor"]);

            if(profile == null)
            {
                Console.WriteLine("GetProfile returned null.");
                return;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine("Profile:");
            Console.WriteLine(profile.ToJsonString(options));
        }

        
        /// <summary>
        /// Gets profile.
        /// </summary>
        /// <param name="actor"></param>
        /// <returns></returns>
        public static JsonNode? DoGetProfile(string actor)
        {
            using (HttpClient client = new HttpClient())
            {
                string url = $"https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile?actor={actor}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
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