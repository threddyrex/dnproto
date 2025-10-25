using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class GetHandleInfo : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"handle"});
    }


    /// <summary>
    /// Resolves a handle - gets did, didDoc, and pds.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        string handle = arguments["handle"];

        //
        // Send request.
        //
        var resolveHandleInfo = BlueskyClient.ResolveHandleInfo(handle);
        string? jsonData = resolveHandleInfo?.ToJsonString();
        Logger.LogInfo($"Resolve handle info JSON: {jsonData}");
        Logger.LogInfo($"DidDoc: {resolveHandleInfo?.DidDoc}");
    }
}