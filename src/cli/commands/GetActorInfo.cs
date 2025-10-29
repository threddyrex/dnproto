using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class GetActorInfo : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"actor"});
    }


    /// <summary>
    /// Resolves a handle - gets did, didDoc, and pds.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        string actor = arguments["actor"];

        //
        // Send request.
        //
        var resolveHandleInfo = BlueskyClient.ResolveActorInfo(actor);
        string? jsonData = resolveHandleInfo?.ToJsonString();
        Logger.LogInfo($"actor: {resolveHandleInfo?.Actor}");
        Logger.LogInfo($"did: {resolveHandleInfo?.Did}");
        Logger.LogInfo($"did_bsky: {resolveHandleInfo?.Did_Bsky}");
        Logger.LogInfo($"did_http: {resolveHandleInfo?.Did_Http}");
        Logger.LogInfo($"did_dns: {resolveHandleInfo?.Did_Dns}");
        Logger.LogInfo($"handle: {resolveHandleInfo?.Handle}");
        Logger.LogInfo($"pds: {resolveHandleInfo?.Pds}");
        Logger.LogInfo($"did doc: {resolveHandleInfo?.DidDoc}");
    }
}