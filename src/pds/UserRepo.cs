
using dnproto.fs;
using dnproto.log;
using dnproto.repo;
using Microsoft.Extensions.Primitives;

namespace dnproto.pds;




public class UserRepo
{
    private LocalFileSystem _lfs;

    private IDnProtoLogger _logger;

    private PdsDb _db;

    private string _userDid;

    private Func<byte[], byte[]> _commitSigningFunction;

    private SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    
    private UserRepo(LocalFileSystem lfs, IDnProtoLogger logger, PdsDb db, Func<byte[], byte[]> commitSigningFunction, string userDid)
    {
        _lfs = lfs;
        _logger = logger;
        _db = db;
        _commitSigningFunction = commitSigningFunction;
        _userDid = userDid;
    }

    public static UserRepo ConnectUserRepo(LocalFileSystem lfs, IDnProtoLogger logger, PdsDb db, Func<byte[], byte[]> commitSigningFunction, string userDid)
    {
        return new UserRepo(lfs, logger, db, commitSigningFunction, userDid);
    }




    #region STREAM

    /// <summary>
    /// Loads entire repo from our database and writes it to the stream.
    /// For example, this is called by getRepo.
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public async Task WriteToStreamAsync(System.IO.Stream stream)
    {
        await _lock.WaitAsync();
        try
        {
            //
            // Header
            //
            var header = _db.GetRepoHeader();
            if (header == null)
            {
                _logger.LogError("Cannot write MST to stream: repo header is null.");
                return;
            }
            var headerDagCbor = header.ToDagCborObject();
            if (headerDagCbor == null)
            {
                _logger.LogError("Cannot write MST to stream: failed to convert repo header to DagCborObject.");
                return;
            }

            var headerDagCborBytes = headerDagCbor.ToBytes();
            var headerLengthVarInt = VarInt.FromLong((long)headerDagCborBytes.Length);
            await VarInt.WriteVarIntAsync(stream, headerLengthVarInt);
            await stream.WriteAsync(headerDagCborBytes, 0, headerDagCborBytes.Length);


            //
            // Repo Commit
            //
            var repoCommit = _db.GetRepoCommit();
            if (repoCommit == null)
            {
                _logger.LogError("Cannot write MST to stream: repo commit is null.");
                return;
            }

            var repoCommitDagCbor = repoCommit.ToDagCborObject();
            if (repoCommitDagCbor == null)
            {
                _logger.LogError("Cannot write MST to stream: failed to convert repo commit to DagCborObject.");
                return;
            }
            var repoCommitCid = repoCommit.Cid;
            if (repoCommitCid == null)
            {
                _logger.LogError("Cannot write MST to stream: repo commit CID is null.");
                return;
            }

            await WriteBlockAsync(stream, repoCommitCid, repoCommitDagCbor);


            //
            // MST Nodes
            //
            MstDb mst = MstDb.ConnectMstDb(_lfs, _logger, _db);
            var allMstNodes = _db.GetAllMstNodes();
            var allMstEntriesByNode = _db.GetAllMstEntriesByNodeObjectId();
            foreach (MstNode mstNode in allMstNodes)
            {
                if (mstNode.Cid == null)
                {
                    _logger.LogError("Cannot write MST to stream: MST node CID is null.");
                    return;
                }
                
                List<MstEntry> mstEntriesForNode = allMstEntriesByNode.ContainsKey((Guid) mstNode.NodeObjectId!) ? allMstEntriesByNode[(Guid) mstNode.NodeObjectId!] : new List<MstEntry>();
                
                var mstNodeDagCbor = mstNode.ToDagCborObject(mstEntriesForNode);
                if (mstNodeDagCbor == null)
                {
                    _logger.LogError($"Cannot write MST to stream: failed to convert MST node {mstNode.Cid?.Base32} to DagCborObject.");
                    return;
                }
                var mstNodeCid = mstNode.Cid;
                if (mstNodeCid == null)
                {
                    _logger.LogError("Cannot write MST to stream: MST node CID is null.");
                    return;
                }

                await WriteBlockAsync(stream, mstNodeCid, mstNodeDagCbor);
            }

            //
            // Repo records (atproto records - like posts, profiles, etc)
            //
            var repoRecords = _db.GetAllRepoRecords();
            foreach (var repoRecord in repoRecords)
            {
                if (repoRecord.DataBlock == null || repoRecord.Cid == null)
                {
                    _logger.LogError($"Cannot write MST to stream: failed to convert repo record {repoRecord.Cid?.Base32} to DagCborObject.");
                    return;
                }

                await WriteBlockAsync(stream, repoRecord.Cid, repoRecord.DataBlock);
            }

        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Writing one record. The format is [VarInt | CidV1 | DagCborObject] (see Repo.cs)
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="cid"></param>
    /// <param name="dagCbor"></param>
    /// <returns></returns>
    private async Task WriteBlockAsync(System.IO.Stream stream, CidV1 cid, DagCborObject dagCbor)
    {
        var cidBytes = cid.AllBytes;
        var dagCborBytes = dagCbor.ToBytes();
        var blockLengthVarInt = VarInt.FromLong((long)(cidBytes.Length + dagCborBytes.Length));

        await VarInt.WriteVarIntAsync(stream, blockLengthVarInt);
        await CidV1.WriteCidAsync(stream, cid);
        await stream.WriteAsync(dagCborBytes, 0, dagCborBytes.Length);
    }

    #endregion



    #region CREATE

    /// <summary>
    /// Creates a new record in the repo. Takes care of everything (rkey, uri, mst, repo record, etc).
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="record"></param>
    /// <returns></returns>
    public 
        (string? uri, 
        RepoRecord? repoRecord, 
        RepoCommit? repoCommit, 
        string? validationStatus) 
            CreateRecord(string collection, DagCborObject record, string? rkey = null)
    {
        //
        // Create rkey and uri
        //
        if(rkey is null || string.IsNullOrEmpty(rkey))
        {
            rkey = RecordKey.GenerateRkey();
        }
        string fullKey = $"{collection}/{rkey}";
        string uri = $"at://{_userDid}/{collection}/{rkey}";
        _logger.LogInfo($"rkey for new record: {rkey}");
        _logger.LogInfo($"uri for new record: {uri}");


        //
        // REPO RECORD
        //
        record.SetString(new string[] { "$type" }, collection);
        CidV1 recordCid = CidV1.ComputeCidForDagCbor(record)!;
        RepoRecord repoRecord = RepoRecord.FromDagCborObject(recordCid, record);
        _db.InsertRepoRecord(collection, rkey, recordCid, record);


        //
        // MST
        //
        var mst = MstDb.ConnectMstDb(_lfs, _logger, _db);
        (CidV1 originalRootMstNodeCid, 
            CidV1 newRootMstNodeCid, 
            List<Guid> updatedNodeObjectIds) = mst.PutEntry(fullKey, recordCid);


        //
        // REPO COMMIT
        //
        var repoCommit = _db.GetRepoCommit()!;
        repoCommit.SignAndRecomputeCid(newRootMstNodeCid, _commitSigningFunction!);
        _db.InsertUpdateRepoCommit(repoCommit);


        //
        // REPO HEADER
        //
        var repoHeader = _db.GetRepoHeader()!;
        repoHeader.RepoCommitCid = repoCommit.Cid!;
        _db.InsertUpdateRepoHeader(repoHeader);


        //
        // Return everything.
        //
        return (uri, repoRecord, repoCommit, "valid");

    }

    #endregion



    #region PUT

    /// <summary>
    /// Creates (or updates) a new record in the repo. Takes care of everything (rkey, uri, mst, repo record, etc).
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="record"></param>
    /// <returns></returns>
    public 
        (string? uri, 
        RepoRecord? repoRecord, 
        RepoCommit? repoCommit, 
        string? validationStatus) 
            PutRecord(string collection, string rkey, DagCborObject record)
    {
        //
        // Create uri
        //
        string fullKey = $"{collection}/{rkey}";
        string uri = $"at://{_userDid}/{collection}/{rkey}";
        _logger.LogInfo($"rkey for record: {rkey}");
        _logger.LogInfo($"uri for record: {uri}");


        //
        // REPO RECORD
        //
        record.SetString(new string[] { "$type" }, collection);
        CidV1 recordCid = CidV1.ComputeCidForDagCbor(record)!;
        RepoRecord repoRecord = RepoRecord.FromDagCborObject(recordCid, record);
        if(_db.GetRepoRecord(collection, rkey) != null)
        {
            _db.DeleteRepoRecord(collection, rkey);
        }

        _db.InsertRepoRecord(collection, rkey, recordCid, record);


        //
        // MST
        //
        var mst = MstDb.ConnectMstDb(_lfs, _logger, _db);
        (CidV1 originalRootMstNodeCid, 
            CidV1 newRootMstNodeCid, 
            List<Guid> updatedNodeObjectIds) = mst.PutEntry(fullKey, recordCid);


        //
        // REPO COMMIT
        //
        var repoCommit = _db.GetRepoCommit()!;
        repoCommit.SignAndRecomputeCid(newRootMstNodeCid, _commitSigningFunction!);
        _db.InsertUpdateRepoCommit(repoCommit);


        //
        // REPO HEADER
        //
        var repoHeader = _db.GetRepoHeader()!;
        repoHeader.RepoCommitCid = repoCommit.Cid!;
        _db.InsertUpdateRepoHeader(repoHeader);


        //
        // Return everything.
        //
        return (uri, repoRecord, repoCommit, "valid");

    }

    #endregion


    #region GET

    /// <summary>
    /// Gets record by collection and rkey.
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="rkey"></param>
    /// <returns></returns>
    public RepoRecord? GetRecord(string collection, string rkey)
    {
        return _db.GetRepoRecord(collection, rkey);
    }

    #endregion




    #region DELETE

    /// <summary>
    /// Deletes record.
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="rkey"></param>
    /// <returns></returns>
    public 
        (RepoHeader? repoHeader, RepoCommit? repoCommit) 
        DeleteRecord(string collection, string rkey)
    {
        //
        // REPO RECORD
        //
        _db.DeleteRepoRecord(collection, rkey);


        //
        // MST
        //
        var mst = MstDb.ConnectMstDb(_lfs, _logger, _db);
        (CidV1 originalRootMstNodeCid, 
            CidV1 newRootMstNodeCid, 
            List<Guid> updatedNodeObjectIds) = mst.DeleteEntry($"{collection}/{rkey}");


        //
        // REPO COMMIT
        //
        var repoCommit = _db.GetRepoCommit()!;
        repoCommit.SignAndRecomputeCid(newRootMstNodeCid, _commitSigningFunction!);
        _db.InsertUpdateRepoCommit(repoCommit);


        //
        // REPO HEADER
        //
        var repoHeader = _db.GetRepoHeader()!;
        repoHeader.RepoCommitCid = repoCommit.Cid!;
        _db.InsertUpdateRepoHeader(repoHeader);


        //
        // Return
        //
        return (repoHeader, repoCommit);
    }


    #endregion
}