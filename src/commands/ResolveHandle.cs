using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace dnproto.commands
{
    public class ResolveHandle : ICommand
    {
        public HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"handle"});
        }

        public HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>();
        }


        /// <summary>
        /// Resolves a handle to a JSON object.
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public void DoCommand(Dictionary<string, string> arguments)
        {
            if(arguments.ContainsKey("handle") == false)
            {
                throw new ArgumentException("Missing required argument: handle");
            }

            Console.WriteLine("Resolving handle...");

            JsonNode? resolvedHandle = Resolve(arguments["handle"]);

            if(resolvedHandle == null)
            {
                Console.WriteLine("ResolveHandle returned null.");
                return;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine("Handle resolved:");
            Console.WriteLine(resolvedHandle.ToJsonString(options));
        }

        
        /// <summary>
        /// Resolves a handle to a JSON object.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public static JsonNode? Resolve(string handle)
        {
            using (HttpClient client = new HttpClient())
            {
                string url = $"https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle?handle={handle}";
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