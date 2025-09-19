using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class Handle_ResolveInfo : BaseCommand
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
        Dictionary<string, string> resolveHandleInfo = BlueskyClient.ResolveHandleInfo(handle);
        string? jsonData = JsonData.ConvertObjectToJsonString(resolveHandleInfo);


        //
        // Print response.
        //
        if (resolveHandleInfo.Count == 0)
        {
            Logger.LogError("Failed to resolve handle.");
        }
        else
        {
            foreach(var key in resolveHandleInfo.Keys)
            {
                Logger.LogInfo($"{key}: {resolveHandleInfo[key]}");
            }
       }
    }
}