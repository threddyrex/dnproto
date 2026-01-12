

using dnproto.fs;
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
        // Get things
        //
        MstDb mst = MstDb.ConnectMstDb(Pds.LocalFileSystem, Pds.Logger, Pds.PdsDb);
        RepoHeader repoHeader = Pds.PdsDb.GetRepoHeader();
        RepoCommit repoCommit = Pds.PdsDb.GetRepoCommit();

        if(! Pds.PdsDb.RecordExists(collection!, rkey!))
        {
            return Results.Json(new { error = "NotFound", message = "Record not found" }, statusCode: 404);
        }

        var repoRecord = Pds.PdsDb.GetRepoRecord(collection!, rkey!);


        //
        // Get mst nodes
        //
        List<(MstNode node, List<MstEntry> entries)> mstNodes = new List<(MstNode node, List<MstEntry> entries)>();

        foreach(Guid nodeObjectId in mst.WalkEntry($"{collection}/{rkey}"))
        {
            MstNode node = Pds.PdsDb.GetMstNodeByObjectId(nodeObjectId);
            List<MstEntry> entries = Pds.PdsDb.GetMstEntriesForNodeObjectId(nodeObjectId);
            mstNodes.Add((node, entries));
        }

        //
        // Write a CAR file to stream, using "application/vnd.ipld.car" content type
        // CAR format: [header length varint][header dag-cbor][block length varint][cid][data]...
        //
        HttpContext.Response.ContentType = "application/vnd.ipld.car";

        Stream stream = HttpContext.Response.Body;

        // repo header
        var headerDagCbor = repoHeader.ToDagCborObject();
        var headerDagCborBytes = headerDagCbor.ToBytes();
        var headerLengthVarInt = VarInt.FromLong((long)headerDagCborBytes.Length);
        await VarInt.WriteVarIntAsync(stream, headerLengthVarInt);
        await stream.WriteAsync(headerDagCborBytes, 0, headerDagCborBytes.Length);

        // repo commit
        var repoCommitDagCbor = repoCommit.ToDagCborObject();
        var repoCommitCid = repoCommit.Cid;
        await UserRepo.WriteBlockAsync(stream, repoCommitCid!, repoCommitDagCbor);

        // mst nodes
        foreach(var (node, entries) in mstNodes)
        {
            var nodeDagCbor = node.ToDagCborObject(entries);
            var nodeCid = node.Cid;
            await UserRepo.WriteBlockAsync(stream, nodeCid!, nodeDagCbor);
        }

        // record
        await UserRepo.WriteBlockAsync(stream, repoRecord.Cid!, repoRecord.DataBlock);

        return Results.Empty;
    }
}