using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.log;
using dnproto.repo;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoSync_GetBlob : BaseXrpcCommand
{
    public async Task<IResult> GetResponseAsync()
    {
        IncrementStatistics();
        
        //
        // Get cid from query parameters
        //
        string? cid = HttpContext.Request.Query["cid"];
        if(string.IsNullOrEmpty(cid))
        {
            return Results.Json(new { error = "InvalidRequest", message = "Missing cid" }, statusCode: 400);
        }

        //
        // Validate
        //
        if(Pds.PdsDb.BlobExists(cid) == false
            || Pds.blobDb.HasBlobBytes(cid) == false)
        {
            return Results.Json(new { error = "NotFound", message = "Blob not found" }, statusCode: 404);
        }

        //
        // Get blob
        //
        var blob = Pds.PdsDb.GetBlobByCid(cid);
        var blobBytes = Pds.blobDb.GetBlobBytes(cid);

        if(blob == null)
        {
            return Results.Json(new { error = "NotFound", message = "Blob not found" }, statusCode: 404);
        }

        //
        // Construct response
        //
        HttpContext.Response.ContentType = blob.ContentType;
        HttpContext.Response.ContentLength = blob.ContentLength;
        await HttpContext.Response.Body.WriteAsync(blobBytes, 0, blob.ContentLength);

        return Results.Empty;
    }
}