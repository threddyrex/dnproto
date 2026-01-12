

using dnproto.repo;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoSync_GetRecord : BaseXrpcCommand
{
    public async Task<IResult> GetResponseAsync()
    {
        //
        // Get param
        //
        string? collection = HttpContext.Request.Query.ContainsKey("collection") ? (string?) HttpContext.Request.Query["collection"] : null;
        string? rkey = HttpContext.Request.Query.ContainsKey("rkey") ? (string?) HttpContext.Request.Query["rkey"] : null;
        if(collection == null)
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: Param 'collection' is required." }, statusCode: 400);
        }
        if(rkey == null)
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: Param 'rkey' is required." }, statusCode: 400);
        }

        //
        // Get record
        //
        if(! Pds.PdsDb.RecordExists(collection!, rkey!))
        {
            return Results.Json(new { error = "NotFound", message = "Record not found" }, statusCode: 404);
        }
        RepoRecord record = Pds.PdsDb.GetRepoRecord(collection!, rkey!);


        //
        // Write the MST to stream, using "application/vnd.ipld.car" content type
        //
        HttpContext.Response.ContentType = "application/vnd.ipld.car";
        var dagCborBytes = record.DataBlock.ToBytes();
        await HttpContext.Response.Body.WriteAsync(dagCborBytes, 0, dagCborBytes.Length);
        return Results.Empty;
    }
}