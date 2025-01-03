using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands
{
    public class CreateSession : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"username", "password"});
        }

        public override HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>(new string[]{"pds", "authFactorToken"});
        }


        /// <summary>
        /// Create session (log in)
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string pds = arguments.ContainsKey("pds") ? arguments["pds"] : "bsky.social";
            string authFactorToken = CommandLineInterface.GetArgumentValue(arguments, "authFactorToken");
            string username = CommandLineInterface.GetArgumentValue(arguments, "username");
            string password = CommandLineInterface.GetArgumentValue(arguments, "password");
            string url = $"https://{pds}/xrpc/com.atproto.server.createSession";

            Console.WriteLine($"url: {url}");


            //
            // Send request
            //
            JsonNode? session = WebServiceClient.SendRequest(url,
                HttpMethod.Post,
                content: string.IsNullOrEmpty(authFactorToken) ?
                    new StringContent(JsonSerializer.Serialize(new
                        {
                            identifier = username,
                            password = password
                        })) : 
                    new StringContent(JsonSerializer.Serialize(new
                        {
                            identifier = username,
                            password = password,
                            authFactorToken = authFactorToken
                        }))
            );


            //
            // Process response
            //
            LocalStateSession.WriteSessionProperties(new Dictionary<string, string>
                {
                    {"did", JsonReader.GetPropertyValue(session, "did")},
                    {"pds", pds},
                    {"accessJwt", JsonReader.GetPropertyValue(session, "accessJwt")},
                    {"refreshJwt", JsonReader.GetPropertyValue(session, "refreshJwt")}
                }
            );

            WebServiceClient.PrintJsonResponseToConsole(session);
        }
    }
}