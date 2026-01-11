using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.fs;
using dnproto.repo;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands
{
    public class ActivateAccount : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"actor"});
        }


        /// <summary>
        /// Activate an account.
        /// https://docs.bsky.app/docs/api/com-atproto-server-activate-account
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");

            //
            // Load lfs
            //
            LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
            ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);
            SessionFile? session = lfs?.LoadSession(actorInfo);

            if(session == null)
            {
                throw new ArgumentException($"No session found for actor {actor}. Please log in.");
            }

            //
            // Call WS
            //
            string url = $"https://{session?.pds}/xrpc/com.atproto.server.activateAccount";
            JsonNode? response = BlueskyClient.SendRequest(url,
                HttpMethod.Post, 
                accessJwt: session?.accessJwt);


        }
    }
}