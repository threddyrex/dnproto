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
    public class GetSession : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"actor"});
        }


        /// <summary>
        /// Get session.
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");

            //
            // Load actor and session
            //
            ActorInfo? actorInfo = LocalFileSystem?.ResolveActorInfo(actor);
            string? sessionFile = LocalFileSystem?.GetPath_SessionFile(actorInfo);
            SessionFile? session = LocalFileSystem?.LoadSession(actorInfo);

            if(session == null)
            {
                Logger.LogError($"No existing session found for actor '{actor}'. Please log in.");
                return;
            }

            //
            // Call WS
            //
            string url = $"https://{session?.pds}/xrpc/com.atproto.server.getSession";
            JsonNode? returnedSession = BlueskyClient.SendRequest(url,
                HttpMethod.Get, 
                accessJwt: session?.accessJwt);

            if (returnedSession == null)
            {
                Logger.LogError("Returned session is null.");
                return;
            }

            //
            // Print results
            //
            BlueskyClient.PrintJsonResponseToConsole(returnedSession);
        }
    }
}