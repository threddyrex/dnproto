using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

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
            string actor = arguments["actor"];

            Console.WriteLine($"actor: {actor}");

            JsonNode? profile = WebServiceClient.SendRequest($"https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile?actor={actor}",
                HttpMethod.Get);

            if(profile == null)
            {
                Console.WriteLine("GetProfile returned null.");
                return;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine("response:");
            Console.WriteLine(profile.ToJsonString(options));
        }        
   }
}