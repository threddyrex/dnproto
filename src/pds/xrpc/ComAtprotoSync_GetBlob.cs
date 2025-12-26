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
        //
        // Get cid from query parameters
        //
        string? cid = HttpContext.Request.Query["cid"];
        if(string.IsNullOrEmpty(cid))
        {
            return Results.Json(new { error = "InvalidRequest", message = "Missing cid" }, statusCode: 400);
        }

        //
        // Get blob
        //
        var blob = Pds.PdsDb.GetBlobByCid(cid);

        if(blob == null)
        {
            return Results.Json(new { error = "NotFound", message = "Blob not found" }, statusCode: 404);
        }

        //
        // Construct response
        //
        HttpContext.Response.ContentType = blob.ContentType;
        HttpContext.Response.ContentLength = blob.ContentLength;
        await HttpContext.Response.Body.WriteAsync(blob.Bytes, 0, blob.ContentLength);

        return Results.Empty;
    }
}