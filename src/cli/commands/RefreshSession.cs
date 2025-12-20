using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.sdk.repo;
using dnproto.sdk.ws;
using dnproto.sdk.fs;

namespace dnproto.cli.commands
{
    public class RefreshSession : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"actor"});
        }


        /// <summary>
        /// Refresh session.
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
                Logger.LogError($"No session found for actor '{actor}'. Please log in.");
                return;
            }

            //
            // Call WS
            //
            string url = $"https://{session?.pds}/xrpc/com.atproto.server.refreshSession";
            JsonNode? newSession = BlueskyClient.SendRequest(url,
                HttpMethod.Post, 
                accessJwt: session?.refreshJwt);

            if (newSession == null)
            {
                Logger.LogError("New session returned null.");
                return;
            }

            //
            // add pds
            //
            newSession["pds"] = session?.pds;

            //
            // Write to disk
            //
            Logger.LogInfo($"Writing session file: {sessionFile}");
            JsonData.WriteJsonToFile(newSession, sessionFile);


            //
            // Print results
            //
            BlueskyClient.PrintJsonResponseToConsole(newSession);
        }
    }
}