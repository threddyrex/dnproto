

using dnproto.cli;
using dnproto.pds;
using dnproto.repo;


namespace dnproto.cli.commands;

/// <summary>
/// </summary>
public class GetFirehoseEvent : BaseCommand
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
        var pds = Pds.InitializePdsForRun(dataDir, Logger);
        if (pds == null)
        {
            Logger.LogError("Failed to initialize PDS.");
            return;
        }

        //
        // Get firehose event
        //
        var firehoseEvent = pds.PdsDb.GetFirehoseEvent(seq);
        if (firehoseEvent == null)
        {
            Logger.LogError($"Failed to get firehose event with sequence {seq}.");
            return;
        }


        // get json for two objects
        var headerJson = JsonData.ConvertObjectToJsonString(firehoseEvent.Header_DagCborObject.GetRawValue());
        var bodyJson = JsonData.ConvertObjectToJsonString(firehoseEvent.Body_DagCborObject.GetRawValue());
        Logger.LogInfo($"Firehose header: {headerJson}");
        Logger.LogInfo($"Firehose body: {bodyJson}");

        var blocks = firehoseEvent.Body_DagCborObject.SelectObjectValue(["blocks"]);
        if (blocks != null && blocks is byte[])
        {
            using (var blockStream = new MemoryStream((byte[])blocks))
            {
                //
                // We can just walk it like a repo!
                //
                Repo.WalkRepo(
                    blockStream,
                    (repoHeader) =>
                    {
                        Logger.LogInfo($"REPO HEADER:");
                        Logger.LogInfo($"   roots: {repoHeader.RepoCommitCid?.GetBase32()}");
                        Logger.LogInfo($"   version: {repoHeader.Version}");
                        return true;
                    },
                    (repoRecord) =>
                    {
                        Logger.LogInfo($"cid: {repoRecord.Cid.GetBase32()}");
                        Logger.LogInfo($"BLOCK JSON:");
                        Logger.LogInfo($"\n{repoRecord.JsonString}");

                        //
                        // This helps to show *exactly* what the DagCbor objects look like,
                        // including their types, and not just the JSON representation.
                        //
                        Logger.LogTrace($"\n{DagCborObject.GetRecursiveDebugString(repoRecord.DataBlock, 0)}");

                        return true;
                    }
                );
            }
        }
    }
}
