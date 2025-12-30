
using dnproto.log;
using dnproto.pds.db;
using dnproto.repo;

namespace dnproto.pds;


/// <summary>
/// Merkle Search Tree (MST) implementation for PDS.
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


public class Mst
{
    private PdsDb _db;

    private IDnProtoLogger _logger;

    private string? _userDid = null;

    Func<byte[], byte[]>? _commitSigningFunction = null;

    private SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    
    public Mst(PdsDb db, IDnProtoLogger logger, Func<byte[], byte[]> commitSigningFunction, string? userDid = null)
    {
        _db = db;
        _logger = logger;
        _commitSigningFunction = commitSigningFunction;
        _userDid = userDid;
    }


    /// <summary>
    /// Initialize a new repo. Should be called only once during the lifetime of the account.
    /// </summary>
    public void InitializeNewRepo()
    {
        _lock.Wait();
        try
        {
            if(this._commitSigningFunction == null)
            {
                _logger.LogError("Cannot initialize new MST repo: commit signing function is null.");
                return;
            }
            
            //
            // Delete everything
            //
            _db.DeleteRepoCommit();
            _db.DeleteAllMstNodes();
            _db.DeleteAllRepoRecords();
            _db.DeleteRepoHeader();

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
            repoCommit.Did = this._userDid;
            repoCommit.Rev = RecordKey.GenerateTid();
            repoCommit.RootMstNodeCid = mstNode.Cid;
            repoCommit.Version = 3;

            byte[]? repoCommitObjUnsignedBytes = repoCommit.ToDagCborBytes();

            if (repoCommitObjUnsignedBytes == null)
            {
                _logger.LogError("Failed to serialize unsigned repo commit.");
                return;
            }

            var hash = System.Security.Cryptography.SHA256.HashData(repoCommitObjUnsignedBytes);
            
            // Sign the hash
            repoCommit.Signature = this._commitSigningFunction(hash);
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
            _db.InsertMstNode(mstNode);
            _db.InsertUpdateRepoCommit(repoCommit);
            _db.InsertUpdateRepoHeader(repoHeader);
        }
        finally
        {
            _lock.Release();
        }
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