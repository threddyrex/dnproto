

using dnproto.cli;
using dnproto.pds;


namespace dnproto.cli.commands;

/// <summary>
/// </summary>
public class DeleteOauthSessions : BaseCommand
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
        db.DeleteAllOauthSessions();
    }
}
