

using dnproto.cli;
using dnproto.pds;


namespace dnproto.cli.commands;

/// <summary>
/// </summary>
public class ListFirehoseEvents : BaseCommand
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
        // Get firehose event
        //
        var firehoseEvents = db.GetFirehoseEventsForSubscribeRepos(-200000, 1000000);

        foreach (var firehoseEvent in firehoseEvents)
        {
            Logger.LogInfo($"Firehose seq: {firehoseEvent.SequenceNumber}");
        }
    }
}
