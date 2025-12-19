using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.sdk.ws;
using dnproto.sdk.fs;

namespace dnproto.cli.commands;

/// <summary>
/// Create invite code
/// </summary>
public class CreateInviteCodes : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "pds", "adminpassword", "codecount", "usecount" });
    }


    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get parameters
        //
        string? pds = arguments.ContainsKey("pds") ? arguments["pds"] : null;
        string? adminPassword = arguments.ContainsKey("adminpassword") ? arguments["adminpassword"] : null;
        string? codeCountStr = arguments.ContainsKey("codecount") ? arguments["codecount"] : null;
        string? useCountStr = arguments.ContainsKey("usecount") ? arguments["usecount"] : null;

        int useCount;
        int codeCount;
        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(adminPassword) || string.IsNullOrEmpty(codeCountStr) || !int.TryParse(codeCountStr, out codeCount) || string.IsNullOrEmpty(useCountStr) || !int.TryParse(useCountStr, out useCount))
        {
            Logger.LogError("Missing required arguments.");
            return;
        }

        //
        // Call pds
        //
        JsonNode? result = BlueskyClient.CreateInviteCodes(pds, adminPassword, codeCount, useCount);
        BlueskyClient.PrintJsonResponseToConsole(result);
        
    }
}