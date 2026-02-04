using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.log;
using dnproto.repo;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoSync_ListBlobs : BaseXrpcCommand
{
    public async Task<IResult> GetResponseAsync()
    {
        IncrementStatistics();
        
        //
        // Get limit and cursor from query parameters
        //
        string? limitStr = HttpContext.Request.Query["limit"];
        string? cursor = HttpContext.Request.Query["cursor"];
        int limit = 100;
        if(string.IsNullOrEmpty(limitStr) == false)
        {
            int.TryParse(limitStr, out limit);
        }

        //
        // Get blob list
        //
        var blobs = Pds.PdsDb.ListBlobsWithCursor(cursor, limit);

        string? nextCursor = null;
        if(blobs.Count > 0)
        {
            nextCursor = blobs[^1];
        }

        //
        // Construct response
        //
        var responseObj = new
        {
            cids = blobs,
            cursor = nextCursor
        };

        return Results.Json(responseObj, statusCode: 200);
    }
}