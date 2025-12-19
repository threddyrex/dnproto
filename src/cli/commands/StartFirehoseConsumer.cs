using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using dnproto.sdk.repo;
using dnproto.sdk.ws;
using dnproto.sdk.fs;
using dnproto.sdk.firehose;

namespace dnproto.cli.commands;

public class StartFirehoseConsumer : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(["actor"]);
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(["cursor"]);
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
                    if (header == null || message == null)
                    {
                        Logger.LogError("Received empty message.");
                        return false;
                    }

                    Logger.LogInfo($"header: {JsonData.ConvertObjectToJsonString(header.GetRawValue())}");
                    Logger.LogInfo($"message: {JsonData.ConvertObjectToJsonString(message.GetRawValue())}");


                    //
                    // Ok now that we have the message, let's look for a "blocks" key.
                    // "blocks" should be a byte array of records, in repo format.
                    // Since it's in repo format, we can walk it just like a repo.
                    //
                    var blocks = message.SelectObject(["blocks"]);
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
                                    Logger.LogInfo($"headerJson:");
                                    Logger.LogInfo($"{repoHeader.JsonString}");
                                    return true;
                                },
                                (repoRecord) =>
                                {
                                    Logger.LogInfo($"cid: {repoRecord.Cid.GetBase32()}");
                                    Logger.LogInfo($"blockJson:");
                                    Logger.LogInfo($"{repoRecord.JsonString}");

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