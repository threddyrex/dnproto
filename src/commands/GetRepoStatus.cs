using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands
{
    public class GetRepoStatus : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"did"});
        }

        public override HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>(new string[]{"pds"});
        }


        /// <summary>
        /// Gets repo status
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            string pds = arguments.ContainsKey("pds") ? arguments["pds"] : "bsky.social";
            string did = arguments["did"];

            Console.WriteLine($"pds: {pds}");
            Console.WriteLine($"did: {did}");

            JsonNode? repoStatus = WebServiceClient.SendRequest($"https://{pds}/xrpc/com.atproto.sync.getRepoStatus?did={did}",
                HttpMethod.Get);

            WebServiceClient.PrintJsonResponseToConsole(repoStatus);
        }
   }
}