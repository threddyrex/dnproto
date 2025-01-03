using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands
{
    public class CreateSession : ICommand
    {
        public HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"username", "password"});
        }

        public HashSet<string> GetOptionalArguments()
        {
            return new HashSet<string>(new string[]{"pds", "authFactorToken"});
        }


        /// <summary>
        /// Create session (login)
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public void DoCommand(Dictionary<string, string> arguments)
        {
            string pds = arguments.ContainsKey("pds") ? arguments["pds"] : "bsky.social";
            JsonNode? session = DoCreateSession(arguments.ContainsKey("pds") ? arguments["pds"] : "bsky.social", 
                arguments["username"], 
                arguments["password"], 
                arguments.ContainsKey("authFactorToken") ? arguments["authFactorToken"] : null);

            if(session == null)
            {
                Console.WriteLine("CreateSession returned null.");
                return;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine("Session:");
            Console.WriteLine(session.ToJsonString(options));
        }

        
        /// <summary>
        /// Create session (login)
        /// </summary>
        /// <param name="pds"></param>
        /// <param name="did"></param>
        /// <returns></returns>
        public static JsonNode? DoCreateSession(string pds, string username, string password, string? authFactorToken)
        {
            using (HttpClient client = new HttpClient())
            {
                // Setup request
                string url = $"https://{pds}/xrpc/com.atproto.server.createSession";
                var request = new HttpRequestMessage(HttpMethod.Post, url);

                if(string.IsNullOrEmpty(authFactorToken))
                {
                    request.Content = new StringContent(JsonSerializer.Serialize(new
                        {
                            identifier = username,
                            password = password
                        }));
                }
                else
                {
                    request.Content = new StringContent(JsonSerializer.Serialize(new
                        {
                            identifier = username,
                            password = password,
                            authFactorToken = authFactorToken
                        }));
                }

                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                

                // Send
                var response = client.Send(request);

                using (var reader = new StreamReader(response.Content.ReadAsStream()))
                {
                    var responseText = reader.ReadToEnd();
                    var ret = JsonNode.Parse(responseText);

                    string sessionDid = "";
                    string sessionPds = "";
                    string sessionAccessJwt = "";
                    string sessionRefreshJwt = "";

                    if(response.StatusCode == HttpStatusCode.OK && ret != null && ret["did"] != null && ret["accessJwt"] != null && ret["refreshJwt"] != null)
                    {
                        sessionDid = JsonReader.GetPropertyValue(ret, "did");
                        sessionPds = pds;
                        sessionAccessJwt = JsonReader.GetPropertyValue(ret, "accessJwt");
                        sessionRefreshJwt = JsonReader.GetPropertyValue(ret, "refreshJwt");
                    }

                    LocalStateSession.WriteSessionProperties(new Dictionary<string, string>
                    {
                        {"sessionDid", sessionDid},
                        {"sessionPds", sessionPds},
                        {"sessionAccessJwt", sessionAccessJwt},
                        {"sessionRefreshJwt", sessionRefreshJwt}
                    });

                    return ret;
                }
            }
        }
    }
}