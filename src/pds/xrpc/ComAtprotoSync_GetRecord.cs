

using dnproto.fs;
using dnproto.mst;
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
        if(collection is null)
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: Param 'collection' is required." }, statusCode: 400);
        }
        if(rkey is null)
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: Param 'rkey' is required." }, statusCode: 400);
        }

        string fullKey = $"{collection}/{rkey}";

        //
        // Get things
        //
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
        Mst mst = Mst.AssembleTreeFromItems(Pds.PdsDb.GetAllRepoRecordMstItems());
        List<MstNode> mstNodes = mst.FindNodesForKey(fullKey);
        List<MstNode> allNodes = mst.FindAllNodes();


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

        // Convert all mst nodes first so CIDs are computed correctly (depends on full tree structure)
        Dictionary<MstNode, (CidV1, DagCborObject)> mstNodeCache = new Dictionary<MstNode, (CidV1, DagCborObject)>();
        foreach(MstNode node in allNodes)
        {
            RepoMst.ConvertMstNodeToDagCbor(mstNodeCache, node);
        }

        // Write only the nodes on the path to the record (proof chain)
        foreach(MstNode node in mstNodes)
        {
            var (nodeCid, nodeDagCbor) = mstNodeCache[node];
            await UserRepo.WriteBlockAsync(stream, nodeCid!, nodeDagCbor);
        }

        // record
        await UserRepo.WriteBlockAsync(stream, repoRecord.Cid!, repoRecord.DataBlock);

        return Results.Empty;
    }
}