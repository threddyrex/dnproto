using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using dnproto.firehose;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class Firehose_Consume : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>();
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[] { "handle", "pds" });
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
        // Figure out which pds to connect to.
        //
        string? pds = null;
        if (arguments.ContainsKey("handle"))
        {
            Dictionary<string, string> handleInfo = BlueskyClient.ResolveHandleInfo(arguments["handle"]);
            pds = handleInfo.ContainsKey("pds") ? handleInfo["pds"] : null;
        }
        else if (arguments.ContainsKey("pds"))
        {
            pds = arguments["pds"];
        }

        if (string.IsNullOrEmpty(pds))
        {
            Logger.LogError("PDS is null or empty. Cannot consume firehose.");
            return;
        }

        string url = $"wss://{pds}/xrpc/com.atproto.sync.subscribeRepos";

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
                                    Logger.LogInfo($" -----------------------------------------------------------------------------------------------------------");
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
                        Logger.LogError("No blocks found in message.");
                        return false;
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