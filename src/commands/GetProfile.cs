using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands
{
    public class GetProfile : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"actor"});
        }


        /// <summary>
        /// Gets user profile.
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            string actor = arguments["actor"];

            Console.WriteLine($"actor: {actor}");

            JsonNode? profile = WebServiceClient.SendRequest($"https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile?actor={actor}",
                HttpMethod.Get);

            WebServiceClient.PrintJsonResponseToConsole(profile);
        }        
   }
}