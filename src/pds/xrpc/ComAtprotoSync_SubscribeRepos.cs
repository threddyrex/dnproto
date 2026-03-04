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
        IncrementStatistics();
        
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
        // Create a linked cancellation token that we can cancel when the client disconnects
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = linkedCts.Token;

        try
        {
            string? userAgent = HttpContext.Request.Headers.ContainsKey("User-Agent") ? HttpContext.Request.Headers["User-Agent"].ToString() : null;
            string? forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            DateTime startTime = DateTime.UtcNow;

            lock (subscriberCountLock)
            {
                subscriberCount++;
                Pds.Logger.LogInfo($"[FIREHOSE] [START] subCount={subscriberCount} cursorParam={cursorParam} cursor={cursor} ip={forwardedFor} userAgent={userAgent}");
            }

            // Start a background task to handle incoming WebSocket messages (ping/pong/close)
            // This is critical for keeping the connection alive - relays send pings and expect pongs
            var receiveTask = HandleIncomingMessagesAsync(webSocket, linkedCts, forwardedFor);

            while (webSocket.State == WebSocketState.Open && !linkedToken.IsCancellationRequested)
            {
                List<FirehoseEvent> events = Pds.PdsDb.GetFirehoseEventsForSubscribeRepos(cursor);
                foreach (FirehoseEvent ev in events)
                {
                    if (linkedToken.IsCancellationRequested) break;

                    TimeSpan connectionLifetime = DateTime.UtcNow - startTime;
                    Pds.Logger.LogInfo($"[FIREHOSE] [SEND] subCount:{subscriberCount} seq={ev.SequenceNumber} cursor={cursor} lifetime={connectionLifetime.TotalMinutes:F1}min ip={forwardedFor} userAgent={userAgent}");

                    byte[] header = ev.Header_DagCborObject.ToBytes();
                    byte[] body = ev.Body_DagCborObject.ToBytes();

                    byte[] combined = new byte[header.Length + body.Length];
                    Buffer.BlockCopy(header, 0, combined, 0, header.Length);
                    Buffer.BlockCopy(body, 0, combined, header.Length, body.Length);

                    await webSocket.SendAsync(new ArraySegment<byte>(combined), WebSocketMessageType.Binary, true, linkedToken);
                    cursor = ev.SequenceNumber;
                }

                await Task.Delay(1000, linkedToken);
            }

            // Wait for the receive task to complete
            await receiveTask;

            // Log why we exited the loop
            Pds.Logger.LogInfo($"[FIREHOSE] Exited main loop. WebSocket.State={webSocket.State}, CancellationRequested={linkedToken.IsCancellationRequested}");
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

    /// <summary>
    /// Handles incoming WebSocket messages (ping/pong/close).
    /// This is critical for keeping connections alive - relays send pings and expect pongs.
    /// Without this, connections time out after ~3 minutes.
    /// </summary>
    private async Task HandleIncomingMessagesAsync(WebSocket webSocket, CancellationTokenSource linkedCts, string? forwardedFor)
    {
        var buffer = new byte[1024];
        try
        {
            while (webSocket.State == WebSocketState.Open && !linkedCts.Token.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Pds.Logger.LogInfo($"[FIREHOSE] [CLOSE] Client requested close. ip={forwardedFor}");
                    linkedCts.Cancel();
                    break;
                }
                else if (result.MessageType == WebSocketMessageType.Binary || result.MessageType == WebSocketMessageType.Text)
                {
                    // Clients shouldn't send data on this endpoint, but log it if they do
                    Pds.Logger.LogTrace($"[FIREHOSE] Received unexpected {result.MessageType} message ({result.Count} bytes). ip={forwardedFor}");
                }
                // Note: Ping/Pong frames are handled automatically by the WebSocket implementation in ASP.NET Core.
                // The ReceiveAsync method doesn't return ping frames - they're handled at the protocol level.
                // However, we still need to be reading from the socket for this to work.
            }
        }
        catch (WebSocketException ex)
        {
            Pds.Logger.LogTrace($"[FIREHOSE] WebSocketException in receive loop: {ex.Message} ip={forwardedFor}");
            linkedCts.Cancel();
        }
        catch (Exception ex)
        {
            Pds.Logger.LogError($"[FIREHOSE] Unexpected exception in receive loop: {ex.Message} ip={forwardedFor}");
            linkedCts.Cancel();
        }
    }

}
