

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
        // Get record raw bytes (must use original bytes to match CID)
        //
        if(! Pds.PdsDb.RecordExists(collection!, rkey!))
        {
            return Results.Json(new { error = "NotFound", message = "Record not found" }, statusCode: 404);
        }
        var (cid, dagCborBytes) = Pds.PdsDb.GetRepoRecordRawBytes(collection!, rkey!);


        //
        // Write a CAR file to stream, using "application/vnd.ipld.car" content type
        // CAR format: [header length varint][header dag-cbor][block length varint][cid][data]...
        //
        HttpContext.Response.ContentType = "application/vnd.ipld.car";

        // Write CAR header (version 1, roots pointing to the record CID)
        var header = new RepoHeader
        {
            Version = 1,
            RepoCommitCid = cid
        };
        var headerDagCbor = header.ToDagCborObject();
        var headerBytes = headerDagCbor.ToBytes();
        var headerLengthVarInt = VarInt.FromLong((long)headerBytes.Length);
        await VarInt.WriteVarIntAsync(HttpContext.Response.Body, headerLengthVarInt);
        await HttpContext.Response.Body.WriteAsync(headerBytes, 0, headerBytes.Length);

        // Write the record block (length varint + cid + dag-cbor data)
        var cidBytes = cid.AllBytes;
        var blockLengthVarInt = VarInt.FromLong((long)(cidBytes.Length + dagCborBytes.Length));
        await VarInt.WriteVarIntAsync(HttpContext.Response.Body, blockLengthVarInt);
        await HttpContext.Response.Body.WriteAsync(cidBytes, 0, cidBytes.Length);
        await HttpContext.Response.Body.WriteAsync(dagCborBytes, 0, dagCborBytes.Length);

        return Results.Empty;
    }
}