using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace dnproto.commands
{
    public class GetRepoStatus : ICommand
    {
        public HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"did"});
        }

        public HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>(new string[]{"pds"});
        }


        /// <summary>
        /// Gets repo status
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public void DoCommand(Dictionary<string, string> arguments)
        {
            string pds = arguments.ContainsKey("pds") ? arguments["pds"] : "bsky.social";
            JsonNode? repoStatus = DoGetRepoStatus(pds, arguments["did"]);

            if(repoStatus == null)
            {
                Console.WriteLine("GetRepoStatus returned null.");
                return;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine("Repo Status:");
            Console.WriteLine(repoStatus.ToJsonString(options));
        }

        
        /// <summary>
        /// Gets repo status.
        /// </summary>
        /// <param name="pds"></param>
        /// <param name="did"></param>
        /// <returns></returns>
        public static JsonNode? DoGetRepoStatus(string pds, string did)
        {
            using (HttpClient client = new HttpClient())
            {
                string url = $"https://{pds}/xrpc/com.atproto.sync.getRepoStatus?did={did}";
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