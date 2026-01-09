
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




    #region APPLYWRITES



    /// <summary>
    /// Main entry point for making any changes to the repo (create, update, delete).
    /// 
    /// This method updates the following:
    /// 
    ///     1. MST (MstNode, MstEntry)
    ///     2. Repo Record (RepoRecord)
    ///     3. Repo Commit (RepoCommit)
    ///     4. Repo Header (RepoHeader)
    ///     5. Firehose (FirehoseEvent)
    /// 
    /// </summary>
    /// <param name="writes"></param>
    /// <returns></returns>
    public List<ApplyWritesResult> ApplyWrites(List<ApplyWritesOperation> writes)
    {
        lock(this)
        {
            var mst = MstDb.ConnectMstDb(_lfs, _logger, _db);
            List<Guid> allUpdatedNodeObjectIds = new List<Guid>();
            List<ApplyWritesResult> results = new List<ApplyWritesResult>();

            //
            // Loop through operations and do writes.
            //
            foreach(var write in writes)
            {
                _logger.LogInfo($"Applying write operation: {write.Type} on collection: {write.Collection} with rkey: {write.Rkey}");

                string uri = $"at://{_userDid}/{write.Collection}/{write.Rkey}";
                string fullKey = $"{write.Collection}/{write.Rkey}";


                switch(write.Type)
                {
                    //
                    // CREATE/UPDATE
                    //
                    case ApplyWritesType.Create:
                    case ApplyWritesType.Update:

                        if(write.Record is null)
                        {
                            _logger.LogError($"Update operation missing record for collection: {write.Collection} with rkey: {write.Rkey}");
                            continue;
                        }

                        //
                        // REPO RECORD
                        //
                        write.Record.SetString(new string[] { "$type" }, write.Collection);
                        CidV1 recordCid = CidV1.ComputeCidForDagCbor(write.Record)!;

                        if(write.Type == ApplyWritesType.Update && _db.RecordExists(write.Collection, write.Rkey))
                        {
                            _db.DeleteRepoRecord(write.Collection, write.Rkey);
                        }

                        _db.InsertRepoRecord(write.Collection, write.Rkey, recordCid, write.Record);


                        //
                        // MST
                        //
                        (CidV1 originalRootMstNodeCid, 
                            CidV1 newRootMstNodeCid, 
                            List<Guid> updatedNodeObjectIds) = mst.PutEntry(fullKey, recordCid);
                        allUpdatedNodeObjectIds.AddRange(updatedNodeObjectIds);


                        //
                        // REPO COMMIT
                        //
                        // (we don't send the commit for every iteration, but methods in
                        // MstDb require the commit to be updated for each write operation)
                        //
                        var repoCommit = _db.GetRepoCommit()!;
                        repoCommit.SignAndRecomputeCid(newRootMstNodeCid, _commitSigningFunction!);
                        _db.InsertUpdateRepoCommit(repoCommit);


                        //
                        // REPO HEADER
                        //
                        // (we don't send the commit for every iteration, but methods in
                        // MstDb require the commit to be updated for each write operation)
                        //
                        var repoHeader = _db.GetRepoHeader()!;
                        repoHeader.RepoCommitCid = repoCommit.Cid!;
                        _db.InsertUpdateRepoHeader(repoHeader);


                        //
                        // Add to return list
                        //
                        string resultType = write.Type == ApplyWritesType.Create ? ApplyWritesType.CreateResult : ApplyWritesType.UpdateResult;

                        results.Add(new ApplyWritesResult
                        {
                            Type = resultType,
                            Uri = uri,
                            Cid = recordCid,
                            ValidationStatus = "valid"
                        });

                        break;
                    

                    //
                    // DELETE
                    //
                    case ApplyWritesType.Delete:

                        //
                        // REPO RECORD
                        //
                        _db.DeleteRepoRecord(write.Collection, write.Rkey);


                        //
                        // MST
                        //
                        (CidV1 originalRootMstNodeCid1, 
                            CidV1 newRootMstNodeCid1, 
                            List<Guid> updatedNodeObjectIds1) = mst.DeleteEntry($"{write.Collection}/{write.Rkey}");
                        allUpdatedNodeObjectIds.AddRange(updatedNodeObjectIds1);

                        //
                        // REPO COMMIT
                        //
                        // (we don't send the commit for every iteration, but methods in
                        // MstDb require the commit to be updated for each write operation)
                        //
                        var repoCommit_delete = _db.GetRepoCommit()!;
                        repoCommit_delete.SignAndRecomputeCid(newRootMstNodeCid1, _commitSigningFunction!);
                        _db.InsertUpdateRepoCommit(repoCommit_delete);


                        //
                        // REPO HEADER
                        //
                        // (we don't send the commit for every iteration, but methods in
                        // MstDb require the commit to be updated for each write operation)
                        //
                        var repoHeader_delete = _db.GetRepoHeader()!;
                        repoHeader_delete.RepoCommitCid = repoCommit_delete.Cid!;
                        _db.InsertUpdateRepoHeader(repoHeader_delete);

                        //
                        // Add to return list
                        //
                        results.Add(new ApplyWritesResult
                        {
                            Type = ApplyWritesType.DeleteResult,
                            Uri = uri,
                            Cid = null
                        });

                        break;
                }
            }


            //
            // We need only unique node ids
            //
            allUpdatedNodeObjectIds = allUpdatedNodeObjectIds.Distinct().ToList();

            //
            // TODO: FIREHOSE
            //


            //
            // Return
            //
            return results;
        }

    }


    public class ApplyWritesOperation
    {
        public required string Type;
        public required string Collection;
        public required string Rkey;
        public DagCborObject? Record = null;
    }

    public class ApplyWritesResult
    {
        public required string Type;
        public string? Uri = null;
        public CidV1? Cid = null;
        public string? ValidationStatus = null;
    }



    public class ApplyWritesType
    {
        public const string Create = "com.atproto.repo.applyWrites#create";
        public const string Update = "com.atproto.repo.applyWrites#update";
        public const string Delete = "com.atproto.repo.applyWrites#delete";
        public const string CreateResult = "com.atproto.repo.applyWrites#createResult";
        public const string UpdateResult = "com.atproto.repo.applyWrites#updateResult";
        public const string DeleteResult = "com.atproto.repo.applyWrites#deleteResult";
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

}