using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class GetRepoStatus : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"handle", "did", "pds", "outfile"});
    }


    /// <summary>
    /// Gets repo status
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        string? pds = CommandLineInterface.GetArgumentValue(arguments, "pds");
        string? did = CommandLineInterface.GetArgumentValue(arguments, "did");
        string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");

        Logger.LogTrace($"pds: {pds}");
        Logger.LogTrace($"did: {did}");


        //
        // If we're resolving handle, do that now.
        //
        if(string.IsNullOrEmpty(handle) == false)
        {
            Logger.LogTrace("Resolving handle to did.");
            var handleInfo = BlueskyClient.ResolveHandleInfo(handle);

            Logger.LogTrace(JsonData.ConvertObjectToJsonString(handleInfo));

            did = handleInfo.Did;
            pds = handleInfo.Pds;
        }

        Logger.LogTrace($"pds: {pds}");
        Logger.LogTrace($"did: {did}");

        string url = $"https://{pds}/xrpc/com.atproto.sync.getRepoStatus?did={did}";
        Logger.LogTrace($"url: {url}");

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did))
        {
            Logger.LogError("Invalid arguments.");
            return;
        }

        JsonNode? repoStatus = BlueskyClient.SendRequest(url,
            HttpMethod.Get);

        BlueskyClient.PrintJsonResponseToConsole(repoStatus);
        JsonData.WriteJsonToFile(repoStatus, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
    }
}