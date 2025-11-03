using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class GetDidWebRepos : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "dataDir", "stateJsonFile" });
    }



    /// <summary>
    /// Download repos for the didwebs listed in the state file.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
        string? stateJsonFile = CommandLineInterface.GetArgumentValue(arguments, "stateJsonFile");

        Logger.LogTrace($"dataDir: {dataDir}");
        Logger.LogTrace($"stateJsonFile: {stateJsonFile}");
        Logger.LogTrace($"state file exists: {File.Exists(stateJsonFile)}");


        //
        // Load lfs
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);

        //
        // Load state
        //
        JsonObject? stateNode = JsonData.ReadJsonObjectFromFile(stateJsonFile);
        JsonObject? didWebs = stateNode?["firehose"]?["didWebs"]?.AsObject();

        if(didWebs ==  null)
        {
            Logger.LogError("Null didwebs. Returning.");
            return;
        }

        foreach(KeyValuePair<string, JsonNode?> pair in didWebs)
        {
            string? didWeb = pair.Key;
            string? pds = pair.Value?["pds"]?.ToString()?.Replace("https://", "")?.Replace("/", "");
            string? repoFile = lfs?.GetPath_RepoFile(didWeb);

            Logger.LogInfo($"didweb: '{didWeb}'  pds: '{pds}'  repoFile: '{repoFile}'");

            if(File.Exists(repoFile) == false)
            {
                try
                {
                    BlueskyClient.GetRepo(pds, didWeb, repoFile);
                    Thread.Sleep(1000);
                }
                catch(Exception ex)
                {
                    Exception? inner = ex;
                    int count = 1;
                    while (inner != null)
                    {
                        Logger.LogError($"Exception {count}: {inner.Message}");
                        Logger.LogTrace(inner.StackTrace ?? "");
                        inner = inner.InnerException;
                        count++;
                    }
                   
                }
            }
        }
    }
}