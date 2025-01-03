using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands
{
    public class ResolveHandle : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"handle"});
        }

        /// <summary>
        /// Resolves a handle to a JSON object.
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            if(arguments.ContainsKey("handle") == false)
            {
                throw new ArgumentException("Missing required argument: handle");
            }

            string handle = arguments["handle"];

            JsonNode? resolvedHandle = WebServiceClient.SendRequest($"https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle?handle={handle}",
                HttpMethod.Get);

            if(resolvedHandle == null)
            {
                Console.WriteLine("ResolveHandle returned null.");
                return;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine("response:");
            Console.WriteLine(resolvedHandle.ToJsonString(options));
        }
    }
}