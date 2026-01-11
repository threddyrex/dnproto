using System.Net.WebSockets;
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

        Pds.Logger.LogInfo($"WebSocket client connected for subscribeRepos, cursor: {cursorParam}");



        //
        // Stream events
        //
        while (webSocket.State == WebSocketState.Open)
        {
            List<FirehoseEvent> events = Pds.PdsDb.GetFirehoseEventsAfterCursor(cursor);
            foreach (FirehoseEvent ev in events)
            {
                byte[] header = ev.Header_DagCborObject.ToBytes();
                byte[] body = ev.Body_DagCborObject.ToBytes();

                byte[] combined = new byte[header.Length + body.Length];
                Buffer.BlockCopy(header, 0, combined, 0, header.Length);
                Buffer.BlockCopy(body, 0, combined, header.Length, body.Length);

                await webSocket.SendAsync(new ArraySegment<byte>(combined), WebSocketMessageType.Binary, true, CancellationToken.None);
                cursor = ev.SequenceNumber;
            }

            await Task.Delay(1000);

            
        }
    }

}
