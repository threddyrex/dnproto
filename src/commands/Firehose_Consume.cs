using System.Net.Http;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using dnproto.repo;
using dnproto.utils;

namespace dnproto.commands;

public class Firehose_Consume : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>();
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"handle", "pds"});
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
        DoCommandAsync(arguments).Wait();
    }

    public static async Task DoCommandAsync(Dictionary<string, string> arguments)
    {
        //
        // Get arguments.
        //
        string? pds = null;
        if (arguments.ContainsKey("handle"))
        {
            Dictionary<string, string> handleInfo = BlueskyUtils.ResolveHandleInfo(arguments["handle"], useBlueskyApi: true);
            pds = handleInfo.ContainsKey("pds") ? handleInfo["pds"] : null;
        }
        else if (arguments.ContainsKey("pds"))
        {
            pds = arguments["pds"];
        }

        if (string.IsNullOrEmpty(pds))
        {
            Console.WriteLine("PDS is null or empty. Cannot consume firehose.");
            return;
        }

        //
        // Set up WS connection
        //
        Console.WriteLine($"Connecting to PDS: {pds}");
        string wsUrl = $"wss://{pds}/xrpc/com.atproto.sync.subscribeRepos";
        Console.WriteLine($"Connecting to wsUrl: {wsUrl}");


        using (ClientWebSocket ws = new ClientWebSocket())
        {
            try
            {
                ws.Options.SetRequestHeader("User-Agent", "dnproto/1.0");
                ws.Options.SetRequestHeader("Accept", "application/json");
                ws.Options.SetRequestHeader("Content-Type", "application/json");

                Uri uri = new Uri(wsUrl);
                await ws.ConnectAsync(uri, CancellationToken.None);

                Console.WriteLine("Connected to PDS firehose.");

                //
                // Listen for messages
                //
                while (ws.State == WebSocketState.Open)
                {
                    ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024]);
                    WebSocketReceiveResult? result = null;

                    using (var ms = new MemoryStream())
                    {
                        //
                        // Read until the end of the message.
                        //
                        do
                        {
                            result = await ws.ReceiveAsync(buffer, CancellationToken.None);

                            if (result.Count > 0 && buffer.Array != null)
                            {
                                ms.Write(buffer.Array, buffer.Offset, result.Count);
                            }
                        } while (!result.EndOfMessage && ws.State == WebSocketState.Open);


                        //
                        // Reset memory stream
                        //
                        ms.Seek(0, SeekOrigin.Begin);

                        //
                        // Read header
                        //
                        DagCborObject? header = DagCborObject.ReadFromStream(ms);

                        if (header == null)
                        {
                            Console.WriteLine("Received empty message.");
                            continue;
                        }
                        Console.WriteLine($"Received header: {JsonData.ConvertObjectToJsonString(header.GetRawValue())}");

                        //
                        // Read body
                        //
                        DagCborObject? body = DagCborObject.ReadFromStream(ms);

                        if (body == null)
                        {
                            Console.WriteLine("Received empty body.");
                            continue;
                        }
                        Console.WriteLine($"Received body: {JsonData.ConvertObjectToJsonString(body.GetRawValue())}");


                        //
                        // Ok now that we have the body, let's look for a "blocks" key.
                        // "blocks" should be a byte array of records, in repo format.
                        // Since it's in repo format, we can walk it just like a repo.
                        //
                        var blocks = body.SelectObject(["blocks"]);
                        if (blocks != null && blocks is byte[])
                        {
                            int totalRecords = 0;
                            Dictionary<string, int> dagCborTypeCounts = new Dictionary<string, int>();
                            Dictionary<string, int> recordTypeCounts = new Dictionary<string, int>();

                            using (var blockStream = new MemoryStream((byte[])blocks))
                            {
                                //
                                // We can just walk it like a repo!
                                //
                                Repo.WalkRepo(
                                    blockStream,
                                    (repoHeader) =>
                                    {
                                        Console.WriteLine($"headerJson:");
                                        Console.WriteLine();
                                        Console.WriteLine($"{repoHeader.JsonString}");
                                        Console.WriteLine();
                                        return true;
                                    },
                                    (repoRecord) =>
                                    {
                                        Console.WriteLine($" -----------------------------------------------------------------------------------------------------------");
                                        Console.WriteLine($"cid: {repoRecord.Cid.GetBase32()}");
                                        Console.WriteLine();
                                        Console.WriteLine();
                                        Console.WriteLine($"blockJson:");
                                        Console.WriteLine();
                                        Console.WriteLine($"{repoRecord.JsonString}");
                                        Console.WriteLine();

                                        // For stats
                                        totalRecords++;
                                        string typeString = repoRecord.DataBlock.Type.GetMajorTypeString();
                                        if (dagCborTypeCounts.ContainsKey(typeString))
                                        {
                                            dagCborTypeCounts[typeString]++;
                                        }
                                        else
                                        {
                                            dagCborTypeCounts[typeString] = 1;
                                        }

                                        string recordType = repoRecord.RecordType ?? "null";

                                        if (recordTypeCounts.ContainsKey(recordType))
                                        {
                                            recordTypeCounts[recordType] = recordTypeCounts[recordType] + 1;
                                        }
                                        else
                                        {
                                            recordTypeCounts[recordType] = 1;
                                        }

                                        return true;
                                    }
                                );
                            }
                        }
                    }

                    //
                    // Check if we're closed.
                    //
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("WebSocket closed.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

}