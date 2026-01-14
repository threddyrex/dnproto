

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
        var pds = Pds.InitializePdsForRun(dataDir, Logger);
        if (pds == null)
        {
            Logger.LogError("Failed to initialize PDS.");
            return;
        }

        //
        // Get firehose event
        //
        var firehoseEvents = pds.PdsDb.GetFirehoseEventsForSubscribeRepos(0, 1000);

        foreach (var firehoseEvent in firehoseEvents)
        {
            Logger.LogInfo($"Firehose seq: {firehoseEvent.SequenceNumber}");
        }
    }
}
