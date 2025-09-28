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

        public override HashSet<string> GetOptionalArguments()
        {
            return ["sessionFile", "pds", "password", "authFactorToken"];
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
            // Find session.
            //
            JsonNode? session = null;
            string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");
            string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");

            if (arguments.ContainsKey("sessionFile"))
            {
                session = JsonData.ReadJsonFromFile(CommandLineInterface.GetArgumentValue(arguments, "sessionFile"));
            }
            else if (CommandLineInterface.HasArgument(arguments, "handle") &&
                     CommandLineInterface.HasArgument(arguments, "password"))
            {
                string? password = CommandLineInterface.GetArgumentValue(arguments, "password");
                string? authFactorToken = CommandLineInterface.GetArgumentValue(arguments, "authFactorToken");

                session = BlueskyClient.CreateSession(handle, password, authFactorToken);
            }
            else
            {
                Logger.LogError("Please provide either a session file (sessionFile), or the handle/password to log in.");
                return;
            }


            //
            // Get values from session
            //
            string? accessJwt = JsonData.SelectString(session, "accessJwt");
            string? pds = JsonData.SelectString(session, "pds");
            string? did = JsonData.SelectString(session, "did");

            Logger.LogInfo($"pds: {pds}");
            Logger.LogInfo($"did: {did}");

            if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(accessJwt) || string.IsNullOrEmpty(did))
            {
                Logger.LogError("Session not found. Please log in.");
                return;
            }

            string url = $"https://{pds}/xrpc/app.bsky.actor.getPreferences";
            Logger.LogInfo($"url: {url}");


            //
            // Get local filepath
            //
            string? preferencesFile = LocalFileSystem.Initialize(dataDir, Logger)?.GetPath_Preferences(handle);
            if (preferencesFile == null)
            {
                Logger.LogError("Failed to initialize local file system.");
                return;
            }



            //
            // Call WS
            //
            Logger.LogInfo($"Writing preferences to: {preferencesFile}");
            JsonNode? response = BlueskyClient.SendRequest(url,
                HttpMethod.Get, 
                accessJwt: accessJwt,
                outputFilePath: preferencesFile);

        }
    }
}