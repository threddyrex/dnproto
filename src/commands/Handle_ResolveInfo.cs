using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands
{
    public class Handle_ResolveInfo : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"handle"});
        }

        public override HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>(new string[]{"outfile"});
        }


        /// <summary>
        /// Resolves a handle to a JSON object.
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments.
            //
            if(arguments.ContainsKey("handle") == false)
            {
                throw new ArgumentException("Missing required argument: handle");
            }

            string handle = arguments["handle"];
            string url = $"https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle?handle={handle}";

            Console.WriteLine($"handle: {handle}");
            Console.WriteLine($"url: {url}");

            //
            // Send request.
            //
            Dictionary<string, string> resolveHandleInfo = DoResolveHandleInfo(handle);

            string jsonData = JsonData.GetObjectJsonString(resolveHandleInfo);


            //
            // Print response.
            //
            Console.WriteLine("");
            Console.WriteLine(jsonData);
            Console.WriteLine("");

            JsonData.WriteJsonToFile(jsonData, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
        }

        public static Dictionary<string, string> DoResolveHandleInfo(string handle)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();

            string url = $"https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle?handle={handle}";
            string? did = null;
            string? didDoc = null;

            ret["url_resolveHandle"] = url;


            //
            // Handle -> did
            //
            JsonNode? response = WebServiceClient.SendRequest(url,
                HttpMethod.Get);

            if(response == null || string.IsNullOrEmpty(JsonData.GetPropertyValue(response, "did")))
            {
                return ret;
            }

            did = JsonData.GetPropertyValue(response, "did");

            if(string.IsNullOrEmpty(did))
            {
                return ret;
            }

            ret["did"] = did;


            //
            // did -> didDoc
            //
            if(did.StartsWith("did:plc"))
            {
                string url_plc = $"https://plc.directory/{did}";
                ret["url_plc"] = url_plc;
                response = WebServiceClient.SendRequest(url_plc,
                    HttpMethod.Get);

                didDoc = WebServiceClient.GetResponseJsonString(response);

                if(didDoc != null)
                {
                    ret["didDoc"] = didDoc;
                }
            }
            else if(did.StartsWith("did:web"))
            {
                string hostname = did.Replace("did:web:", "");

                string url_didweb = $"https://{hostname}/.well-known/did.json";
                ret["url_didweb"] = url_didweb;

                response = WebServiceClient.SendRequest(url_didweb, HttpMethod.Get);

                didDoc = WebServiceClient.GetResponseJsonString(response);

                if(didDoc != null)
                {
                    ret["didDoc"] = didDoc;
                }
            }

            //
            // didDoc -> pds
            //
            if(string.IsNullOrEmpty(didDoc))
            {
                return ret;
            }

            JsonNode? didDocJson = JsonNode.Parse(didDoc);

            if(didDocJson != null)
            {
                string pds = JsonData.GetValueAtPath(didDocJson, new string [] {"service", "serviceEndpoint"});
                ret["pds"] = pds.Replace("https://", "");
            }

            return ret;

        }
    }
}