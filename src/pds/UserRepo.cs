
using dnproto.log;
using dnproto.pds.db;
using dnproto.repo;

namespace dnproto.pds;


/// <summary>
/// Repo implementation for PDS, including MST.
/// </summary>
/*
    ✅ RepoHeader (only one)
        CidV1 RepoCommitCid (points to repocomitcid)
        Int Version

    ✅ RepoCommit (only one)
        Int Version (always 3)
        CidV1 Cid;
        CidV1 RootMstNodeCid;
        String Rev (increases monotonically, typically timestamp)
        CidV1? PrevMstNodeCid (usually null)
        String Signature (base 64 encoded)

    ✅ MstNode (0 or more)
        CidV1 Cid
        "l" - String? LeftMstNodeCid

    ✅ MstEntry (0 or more)
        CidV1 MstNodeCid
        "k" - String KeySuffix (plaintext - we can convert to base64 later)
        "p" - Int PrefixLength
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

        LoadFromDb();
    }


    //
    // The following items are the in-memory representation of the repo.
    // We keep everything in memory, backed by SQL.
    // When changes occur, we update both in-memory and SQL.
    //
    public RepoHeader? RepoHeader = null;
    public RepoCommit? RepoCommit = null;
    public Dictionary<CidV1, MstNode> MstNodes = new Dictionary<CidV1, MstNode>();
    public Dictionary<CidV1, RepoRecord> RepoRecords = new Dictionary<CidV1, RepoRecord>();

    private void LoadFromDb()
    {
        _logger.LogInfo("Loading PDS repo from database...");

        //
        // Load repo header
        //
        RepoHeader = _db.GetRepoHeader();

        //
        // Load repo commit
        //
        RepoCommit = _db.GetRepoCommit();

        //
        // Load MST nodes
        //
        var mstNodes = _db.GetAllMstNodes();
        MstNodes.Clear();
        foreach (var mstNode in mstNodes)
        {
            if (mstNode.Cid != null)
            {
                MstNodes[mstNode.Cid] = mstNode;
            }
            else
            {
                _logger.LogError("Found MST node with null CID in database.");
            }
        }

        //
        // Load repo records
        //
        var repoRecords = _db.GetAllRepoRecords();
        RepoRecords.Clear();
        foreach (var repoRecord in repoRecords)
        {
            if (repoRecord.Cid != null)
            {
                RepoRecords[repoRecord.Cid] = repoRecord;
            }
            else
            {
                _logger.LogError("Found repo record with null CID in database.");
            }
        }

        //
        // Print
        //
        _logger.LogInfo("");
        _logger.LogInfo($"Loaded PDS repo.");
        _logger.LogInfo($"  RepoHeader={(RepoHeader != null ? RepoHeader.RepoCommitCid?.ToString() ?? "null" : "null")}");
        _logger.LogInfo($"  RepoCommit={(RepoCommit != null ? RepoCommit.Cid?.ToString() ?? "null" : "null")}");
        _logger.LogInfo($"  MstNodes={MstNodes.Count}");
        _logger.LogInfo($"  RepoRecords={RepoRecords.Count}");
        _logger.LogInfo("");
    }


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
        db.DeleteAllRepoRecords();
        db.DeleteRepoHeader();

        //
        // Create Mst Node
        //
        var mstNode = new MstNode
        {
            Cid = null, // to be set
            LeftMstNodeCid = null
        };

        mstNode.Cid = CidV1.ComputeCidForDagCbor(mstNode.ToDagCborObject());


        //
        // Create repo commit.
        // First create unsigned, then sign it.
        //
        var repoCommit = new RepoCommit();
        repoCommit.Did = userDid;
        repoCommit.Rev = RecordKey.GenerateTid();
        repoCommit.RootMstNodeCid = mstNode.Cid;
        repoCommit.Version = 3;

        byte[]? repoCommitObjUnsignedBytes = repoCommit.ToDagCborBytes();

        if (repoCommitObjUnsignedBytes == null)
        {
            logger.LogError("Failed to serialize unsigned repo commit.");
            return;
        }

        var hash = System.Security.Cryptography.SHA256.HashData(repoCommitObjUnsignedBytes);
        
        // Sign the hash
        repoCommit.Signature = commitSigningFunction(hash);
        byte[]? repoCommitObjSignedBytes = repoCommit.ToDagCborBytes();
        repoCommit.Cid = CidV1.ComputeCidForDagCbor(repoCommit.ToDagCborObject()!);


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
        db.InsertMstNode(mstNode);
        db.InsertUpdateRepoCommit(repoCommit);
        db.InsertUpdateRepoHeader(repoHeader);
    }

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
            var mstNodes = _db.GetAllMstNodes();
            foreach (var mstNode in mstNodes)
            {
                var mstNodeDagCbor = mstNode.ToDagCborObject();
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
}