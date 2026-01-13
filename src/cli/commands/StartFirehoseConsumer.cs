using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using dnproto.repo;
using dnproto.ws;
using dnproto.fs;
using dnproto.firehose;

namespace dnproto.cli.commands;

public class StartFirehoseConsumer : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(["actor"]);
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(["cursor", "showDagCborTypes"]);
    }


    /// <summary>
    /// Listens to firehose and prints out what it sees.
    /// If you specify a handle, it will resolve the handle to a PDS and connect to that PDS.
    /// If you specify a PDS, it will connect to that PDS.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Args
        //
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
        string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");
        string? cursorStr = CommandLineInterface.GetArgumentValue(arguments, "cursor");
        string? showDagCborTypesStr = CommandLineInterface.GetArgumentValue(arguments, "showDagCborTypes");
        bool showDagCborTypes = !string.IsNullOrEmpty(showDagCborTypesStr) && bool.TryParse(showDagCborTypesStr, out bool result) && result;

        //
        // Load lfs
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
        ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

        if (actorInfo == null)
        {
            Logger.LogError("Failed to resolve actor info.");
            return;
        }



        string? pds = actorInfo.Pds;

        string url = $"wss://{pds}/xrpc/com.atproto.sync.subscribeRepos";

        if(!string.IsNullOrEmpty(cursorStr) && int.TryParse(cursorStr, out int cursor))
        {
            url += $"?cursor={cursorStr}";
        }


        Logger.LogInfo($"Connecting to firehose at: {url}");
        
        //
        // Listen on firehose
        //
        try
        {
            Firehose.Listen(
                url,
                (header, message) =>
                {
                    //
                    // Check header and message
                    //
                    Logger.LogInfo($" -----------------------------------------------------------------------------------------------------------");
                    Logger.LogInfo($" NEW FIREHOSE FRAME");
                    Logger.LogInfo($" -----------------------------------------------------------------------------------------------------------");
                    if (header == null || message == null)
                    {
                        Logger.LogError("Received empty message.");
                        return false;
                    }

                    Logger.LogInfo($"DAG CBOR OBJECT 1 (HEADER):\n{JsonData.ConvertObjectToJsonString(header.GetRawValue())}");
                    Logger.LogInfo($"DAG CBOR OBJECT 2 (MESSAGE):\n{JsonData.ConvertObjectToJsonString(message.GetRawValue())}");

                    Logger.LogInfo(" PARSING BLOCKS");

                    //
                    // Ok now that we have the message, let's look for a "blocks" key.
                    // "blocks" should be a byte array of records, in repo format.
                    // Since it's in repo format, we can walk it just like a repo.
                    //
                    var blocks = message.SelectObjectValue(["blocks"]);
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
                                    if(showDagCborTypes)
                                    {
                                        Logger.LogTrace($"\n{DagCborObject.GetRecursiveDebugString(repoRecord.DataBlock, 0)}");
                                    }

                                    return true;
                                }
                            );
                        }
                    }
                    else
                    {
                        Logger.LogInfo("No blocks found in message.");
                    }

                    return true;
                }
            );
        }
        catch (Exception ex)
        {
            Logger.LogError($"{ex.Message}");
        }
    }
}