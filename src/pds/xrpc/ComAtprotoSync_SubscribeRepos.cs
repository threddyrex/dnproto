using System.Net.WebSockets;
using dnproto.log;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


/// <summary>
/// Implements the com.atproto.sync.subscribeRepos endpoint.
/// This is a WebSocket endpoint that streams firehose events to connected clients.
/// 
/// https://atproto.com/specs/event-stream
/// 
/// Each WebSocket frame contains two DAG-CBOR objects concatenated together:
/// 1. Header - indicates message type (op and t fields)
/// 2. Body - the actual message content
/// 
/// Query parameters:
/// - cursor: Optional sequence number to start streaming from (exclusive)
/// 
/// </summary>
public class ComAtprotoSync_SubscribeRepos : BaseXrpcCommand
{

    private static int subscriberCount = 0;
    private static readonly object subscriberCountLock = new object();


    /// <summary>
    /// Handles the WebSocket connection for subscribeRepos.
    /// </summary>
    public async Task HandleWebSocketAsync()
    {
        //
        // Check if this is a WebSocket request
        //
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsync("WebSocket connection required");
            return;
        }

        //
        // Parse cursor from query string
        //
        long cursor = Pds.PdsDb.GetMostRecentlyUsedSequenceNumber();
        string? cursorParam = HttpContext.Request.Query["cursor"];
        if (!string.IsNullOrEmpty(cursorParam) && long.TryParse(cursorParam, out long parsedCursor))
        {
            cursor = parsedCursor;
        }

        //
        // Accept the WebSocket connection
        //
        using WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

        // Use the request's cancellation token to handle graceful shutdown
        CancellationToken cancellationToken = HttpContext.RequestAborted;

        //
        // Stream events
        //
        try
        {
            string? userAgent = HttpContext.Request.Headers.ContainsKey("User-Agent") ? HttpContext.Request.Headers["User-Agent"].ToString() : null;

            lock (subscriberCountLock)
            {
                subscriberCount++;
                Pds.Logger.LogInfo($"[FIREHOSE] WebSocket client connected for subscribeRepos. cursorParam:{cursorParam} cursor:{cursor} userAgent:{userAgent} subscriberCount:{subscriberCount}");
            }


            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                List<FirehoseEvent> events = Pds.PdsDb.GetFirehoseEventsForSubscribeRepos(cursor);
                foreach (FirehoseEvent ev in events)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    Pds.Logger.LogInfo($"[FIREHOSE] Sending firehose event. seq:{ev.SequenceNumber} cursor:{cursor} userAgent:{userAgent} subscriberCount:{subscriberCount}");

                    byte[] header = ev.Header_DagCborObject.ToBytes();
                    byte[] body = ev.Body_DagCborObject.ToBytes();

                    byte[] combined = new byte[header.Length + body.Length];
                    Buffer.BlockCopy(header, 0, combined, 0, header.Length);
                    Buffer.BlockCopy(body, 0, combined, header.Length, body.Length);

                    await webSocket.SendAsync(new ArraySegment<byte>(combined), WebSocketMessageType.Binary, true, cancellationToken);
                    cursor = ev.SequenceNumber;
                }

                await Task.Delay(1000, cancellationToken);
            }

            // Log why we exited the loop
            Pds.Logger.LogInfo($"[FIREHOSE] Exited main loop. WebSocket.State={webSocket.State}, CancellationRequested={cancellationToken.IsCancellationRequested}");
        }
        catch (OperationCanceledException ex)
        {
            Pds.Logger.LogTrace($"[FIREHOSE] OperationCanceledException caught (graceful shutdown) {ex.Message}");
            Pds.Logger.LogTrace($"[FIREHOSE] Stack trace: {ex.StackTrace}");
        }
        catch (Exception ex)
        {
            Pds.Logger.LogError($"[FIREHOSE] Unexpected exception: {ex.Message}");
            Pds.Logger.LogError($"[FIREHOSE] Stack trace: {ex.StackTrace}");
        }
        finally
        {
            lock (subscriberCountLock)
            {
                subscriberCount--;
            }
        }

        // Close the WebSocket gracefully if still open
        if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
        {
            try
            {
                using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", closeTimeout.Token);
            }
            catch
            {
                // Ignore errors during close - we're shutting down anyway
            }
        }

        Pds.Logger.LogTrace("[FIREHOSE] WebSocket client disconnected or process is shutting down.");
    }

}
