using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands
{
    public class GetRepo : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"pds", "did", "outputFilePath"});
        }


        /// <summary>
        /// Downloads a user's repository.
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string pds = CommandLineInterface.GetArgumentValue(arguments, "pds");
            string did = CommandLineInterface.GetArgumentValue(arguments, "did");
            string outputFilePath = CommandLineInterface.GetArgumentValue(arguments, "outputFilePath");
            string url = $"https://{pds}/xrpc/com.atproto.sync.getRepo?did={did}";

            Console.WriteLine($"pds: {pds}");
            Console.WriteLine($"did: {did}");
            Console.WriteLine($"outputFilePath: {outputFilePath}");
            Console.WriteLine($"url: {url}");

            if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did) || string.IsNullOrEmpty(outputFilePath))
            {
                Console.WriteLine("Invalid arguments.");
                return;
            }


            //
            // Call WS
            //
            WebServiceClient.SendRequest(url,
                HttpMethod.Get, 
                outputFilePath: outputFilePath);

        }
    }
}