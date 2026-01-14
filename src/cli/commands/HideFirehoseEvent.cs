

using dnproto.cli;
using dnproto.pds;
using dnproto.repo;


namespace dnproto.cli.commands;

/// <summary>
/// </summary>
public class HideFirehoseEvent : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"seq"});
    }

    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
        string? seqStr = CommandLineInterface.GetArgumentValue(arguments, "seq");
        int seq;

        if (string.IsNullOrEmpty(dataDir) || string.IsNullOrEmpty(seqStr) || !int.TryParse(seqStr, out seq))
        {
            Logger.LogError("dataDir and seq arguments are required to run PDS.");
            return;
        }
        
        //
        // Init pds
        //
        PdsDb db = PdsDb.ConnectPdsDb(LocalFileSystem!, Logger);

        //
        // Hide firehose event
        //
        db.HideFirehoseEvent(seq);
        Logger.LogInfo($"Firehose event with sequence {seq} hidden.");
    }
}
