

using dnproto.cli;
using dnproto.pds;


namespace dnproto.cli.commands;

/// <summary>
/// </summary>
public class ListOauthSessions : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{});
    }

    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");

        if (string.IsNullOrEmpty(dataDir))
        {
            Logger.LogError("dataDir argument is required to run PDS.");
            return;
        }
        
        //
        // Init pds
        //
        PdsDb db = PdsDb.ConnectPdsDb(LocalFileSystem!, Logger);

        //
        // Get oauth sessions
        //
        var oauthSessions = db.GetAllOauthSessions();
        foreach (var session in oauthSessions)
        {
            Logger.LogInfo("");
            Logger.LogInfo($"createdDate={session.CreatedDate}");
            Logger.LogInfo($"clientId={session.ClientId}");
            Logger.LogInfo($"scope={session.Scope}");
            Logger.LogInfo($"refreshTokenExpiresDate={session.RefreshTokenExpiresDate}");
            Logger.LogInfo($"sessionId={session.SessionId}");
            Logger.LogInfo("");
        }
    }
}
