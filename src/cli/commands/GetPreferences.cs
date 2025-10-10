using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands
{
    public class GetPreferences : BaseCommand
    {
        public override HashSet<string> GetRequiredArguments()
        {
            return new HashSet<string>(new string[]{"dataDir", "handle"});
        }



        /// <summary>
        /// Get preferences for the current session.
        /// Contains things like muted words, saved feeds, etc. 
        /// https://docs.bsky.app/docs/api/app-bsky-actor-get-preferences
        /// </summary>
        /// <param name="arguments"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void DoCommand(Dictionary<string, string> arguments)
        {
            //
            // Get arguments
            //
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
            string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");

            //
            // Load session
            //
            LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
            SessionFile? session = lfs?.LoadSession(handle);
            if (session == null)
            {
                Logger.LogError($"Failed to load session for handle: {handle}");
                return;
            }


            //
            // Get local filepath
            //
            string? preferencesFile = lfs?.GetPath_Preferences(handle);
            if (preferencesFile == null)
            {
                Logger.LogError("Failed to initialize local file system.");
                return;
            }



            //
            // Call WS
            //
            Logger.LogInfo($"Writing preferences to: {preferencesFile}");
            string url = $"https://{session.pds}/xrpc/app.bsky.actor.getPreferences";
            JsonNode? response = BlueskyClient.SendRequest(url,
                HttpMethod.Get, 
                accessJwt: session.accessJwt,
                outputFilePath: preferencesFile);

        }
    }
}