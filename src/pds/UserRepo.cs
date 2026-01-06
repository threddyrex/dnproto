
using dnproto.log;
using dnproto.pds.db;
using dnproto.repo;

namespace dnproto.pds;


/// <summary>
/// Repo implementation for PDS, including MST.
/// </summary>
/*
    ✅ RepoHeader (only one)
        CidV1 RepoCommitCid (points to RepoCommit)
        Int Version

    ✅ RepoCommit (only one)
        int Version (always 3)
        CidV1 Cid;
        CidV1 RootMstNodeCid;
        string Rev (increases monotonically, typically timestamp)
        CidV1? PrevMstNodeCid (usually null)
        string Signature

    ✅ MstNode (0 or more)
        CidV1 Cid
        "l" - CidV1? LeftMstNodeCid (optional to a sub-tree node)

    ✅ MstEntry (0 or more)
        CidV1 MstNodeCid
        int EntryIndex (0-based index within parent MstNode)
        "k" - string KeySuffix
        "p" - int PrefixLength
        "t" - CidV1? TreeMstNodeCid
        "v" - CidV1 RecordCid (cid of atproto record)

    ✅ RepoRecord (0 or more)
        CidV1 Cid
        DagCborObject Data (the actual atproto record)
*/


public class UserRepo
{
    private PdsDb _db;

    private IDnProtoLogger _logger;

    private string? _userDid = null;

    Func<byte[], byte[]>? _commitSigningFunction = null;

    private SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    
    public UserRepo(PdsDb db, IDnProtoLogger logger, Func<byte[], byte[]> commitSigningFunction, string? userDid = null)
    {
        _db = db;
        _logger = logger;
        _commitSigningFunction = commitSigningFunction;
        _userDid = userDid;
    }

    #region INSTALL

    /// <summary>
    /// Install a new repo. Should be called only once during the lifetime of the account.
    /// </summary>
    public static void InstallRepo(PdsDb db, IDnProtoLogger logger, Func<byte[], byte[]> commitSigningFunction, string userDid)
    {
        if(commitSigningFunction == null)
        {
            logger.LogError("Cannot install new MST repo: commit signing function is null.");
            return;
        }
        
        //
        // Delete everything
        //
        db.DeleteRepoCommit();
        db.DeleteAllMstNodes();
        db.DeleteAllMstEntries();
        db.DeleteAllRepoRecords();
        db.DeleteRepoHeader();

        //
        // Create Mst Node
        //
        var mstNode = new MstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = null, // to be set
            LeftMstNodeCid = null
        };

        mstNode.Cid = CidV1.ComputeCidForDagCbor(mstNode.ToDagCborObject(new List<MstEntry>())!);


        //
        // Create repo commit.
        // First create unsigned, then sign it.
        //
        var repoCommit = new RepoCommit();
        repoCommit.Did = userDid;
        repoCommit.Rev = RecordKey.GenerateTid();
        repoCommit.RootMstNodeCid = mstNode.Cid;
        repoCommit.Version = 3;
        repoCommit.SignRepoCommit(mstNode.Cid, commitSigningFunction);


        //
        // Create repo header
        //
        var repoHeader = new RepoHeader
        {
            RepoCommitCid = repoCommit.Cid,
            Version = 1
        };


        //
        // Insert everything into the database
        //
        db.InsertMstNode(mstNode); // no entries
        db.InsertUpdateRepoCommit(repoCommit);
        db.InsertUpdateRepoHeader(repoHeader);
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
            var mst = new MstDb(_db);
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
            CreateRecord(string collection, DagCborObject record)
    {
        //
        // Create rkey and uri
        //
        string rkey = RecordKey.GenerateRkey(collection);
        string fullKey = $"{collection}/{rkey}";
        string uri = $"at://{_userDid}/{collection}/{rkey}";
        _logger.LogInfo($"Generated rkey for new record: {rkey}");
        _logger.LogInfo($"Generated uri for new record: {uri}");


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
        var mst = new MstDb(_db);
        (CidV1 originalRootMstNodeCid, 
            CidV1 newRootMstNodeCid, 
            List<Guid> updatedNodeObjectIds) = mst.PutEntry(fullKey, recordCid);


        //
        // REPO COMMIT
        //
        var repoCommit = _db.GetRepoCommit()!;
        repoCommit.SignRepoCommit(newRootMstNodeCid, _commitSigningFunction!);
        _db.InsertUpdateRepoCommit(repoCommit);


        //
        // REPO HEADER
        //
        var repoHeader = _db.GetRepoHeader()!;
        repoHeader.RepoCommitCid = repoCommit.Cid;
        _db.InsertUpdateRepoHeader(repoHeader);


        //
        // Return everything.
        //
        return (uri, repoRecord, repoCommit, "valid");

    }

    #endregion
}