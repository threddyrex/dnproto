using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.sdk.ws;
using dnproto.sdk.fs;

namespace dnproto.cli.commands;

/// <summary>
/// Create invite code
/// </summary>
public class CreateAccount : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "pds", "handle", "did", "invitecode", "password" });
    }


    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get parameters
        //
        string? pds = CommandLineInterface.GetArgumentValue(arguments, "pds");
        string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");
        string? did = CommandLineInterface.GetArgumentValue(arguments, "did");
        string? inviteCode = CommandLineInterface.GetArgumentValue(arguments, "invitecode");
        string? password = CommandLineInterface.GetArgumentValue(arguments, "password");

        if (string.IsNullOrEmpty(pds) 
            || string.IsNullOrEmpty(handle) 
            || string.IsNullOrEmpty(did) 
            || string.IsNullOrEmpty(inviteCode) 
            || string.IsNullOrEmpty(password))
        {
            Logger.LogError("Missing required arguments.");
            return;
        }

        //
        // Call pds
        //
        JsonNode? result = BlueskyClient.CreateAccount(pds, handle, did, inviteCode, password);
        BlueskyClient.PrintJsonResponseToConsole(result);
        
    }
}