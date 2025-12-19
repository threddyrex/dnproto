using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.sdk.ws;
using dnproto.sdk.fs;

namespace dnproto.cli.commands;

/// <summary>
/// Create invite code
/// </summary>
public class CreateInviteCode : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "pds", "adminpassword", "usecount" });
    }


    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get parameters
        //
        string? pds = arguments.ContainsKey("pds") ? arguments["pds"] : null;
        string? adminPassword = arguments.ContainsKey("adminpassword") ? arguments["adminpassword"] : null;
        string? useCountStr = arguments.ContainsKey("usecount") ? arguments["usecount"] : null;

        int useCount;
        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(adminPassword) || string.IsNullOrEmpty(useCountStr) || !int.TryParse(useCountStr, out useCount))
        {
            Logger.LogError("Missing required arguments.");
            return;
        }

        //
        // Call pds
        //
        JsonNode? result = BlueskyClient.CreateInviteCode(pds, adminPassword, useCount);
        BlueskyClient.PrintJsonResponseToConsole(result);
        
    }
}