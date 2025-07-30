using System.Net.Http;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using dnproto.repo;


namespace dnproto.firehose;


public class Firehose
{
    /// <summary>
    /// Listens to a Bluesky firehose and sends messages back to caller.
    /// 
    /// https://atproto.com/specs/event-stream#streaming-wire-protocol-v0
    /// 
    ///     "Every WebSocket frame contains two DAG-CBOR objects, 
    ///     with bytes concatenated together: a header (indicating message type), 
    ///     and the actual message."
    /// 
    ///     In the second object (message), there is a property called "blocks"
    ///     that contains a byte array of records, in repo format.
    ///     You can walk this byte array like a repo.
    /// 
    ///     See the repo directory (Repo.cs) for how to walk a repo.
    /// 
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public static void Listen(string url, Func<DagCborObject, DagCborObject, bool> messageCallback)
    {
        if (string.IsNullOrEmpty(url)) return;

        Firehose.ListenAsync(url, messageCallback).Wait();
    }


    public static async Task ListenAsync(string url, Func<DagCborObject, DagCborObject, bool> messageCallback)
    {
        if (string.IsNullOrEmpty(url)) return;

        using (ClientWebSocket ws = new ClientWebSocket())
        {
            ws.Options.SetRequestHeader("User-Agent", "dnproto/1.0");
            ws.Options.SetRequestHeader("Accept", "application/json");
            ws.Options.SetRequestHeader("Content-Type", "application/json");

            Uri uri = new Uri(url);
            await ws.ConnectAsync(uri, CancellationToken.None);

            Console.WriteLine($"Connected to pds firehose: {url}");

            //
            // Listen for messages
            //
            bool keepGoing = true;
            while (ws.State == WebSocketState.Open && keepGoing)
            {
                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024]);
                WebSocketReceiveResult? result = null;

                using (var ms = new MemoryStream())
                {
                    //
                    // Read until the end of the message.
                    // It might arrive in multiple chunks.
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
                    // The first DAG-CBOR object: the header
                    //
                    DagCborObject? header = DagCborObject.ReadFromStream(ms);

                    //
                    // The second DAG-CBOR object: the message
                    //
                    DagCborObject? body = DagCborObject.ReadFromStream(ms);

                    //
                    // Send back to caller
                    //
                    keepGoing = messageCallback(header, body);
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
    }
}