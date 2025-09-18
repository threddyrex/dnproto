using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class Handle_Resolve : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"handle"});
    }


    /// <summary>
    /// Resolves a handle to a JSON object.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        string? did = BlueskyClient.ResolveHandleToDid_ViaBlueskyApi(arguments["handle"]);
        Logger.LogInfo($"{did}");
    }

}