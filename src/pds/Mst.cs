
using dnproto.log;
using dnproto.pds.db;
using dnproto.repo;

namespace dnproto.pds;


/// <summary>
/// Merkle Search Tree (MST) implementation for PDS.
/// </summary>

/*
    ✅ DbRepoHeader (only one)
        CidV1 RepoCommitCid (points to repocomitcid)
        Int Version

    ✅ DbRepoCommit (only one)
        Int Version (always 3)
        CidV1 Cid;
        CidV1 RootMstNodeCid;
        String Rev (increases monotonically, typically timestamp)
        CidV1? PrevMstNodeCid (usually null)
        String Signature (base 64 encoded)

    ✅ DbMstNode (0 or more)
        CidV1 Cid
        "l" - String? LeftMstNodeCid

    ✅ DbMstEntry (0 or more)
        CidV1 MstNodeCid
        "k" - String KeySuffix (plaintext - we can convert to base64 later)
        "p" - Int PrefixLength
        "t" - CidV1? TreeMstNodeCid
        "v" - CidV1 RecordCid (cid of atproto record)

    ✅ DbRepoRecord (0 or more)
        CidV1 Cid
        DagCborObject Data (the actual atproto record)

*/


public class Mst
{
    private PdsDb _db;

    private IDnProtoLogger _logger;

    private string? _userDid = null;

    Func<byte[], byte[]>? _commitSigningFunction = null;
    
    public Mst(PdsDb db, IDnProtoLogger logger, Func<byte[], byte[]> commitSigningFunction, string? userDid = null)
    {
        _db = db;
        _logger = logger;
        _commitSigningFunction = commitSigningFunction;
        _userDid = userDid;
    }


    public void InitializeNewRepo()
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
        var mstNode = new DbMstNode
        {
            Cid = null, // to be set
            LeftMstNodeCid = null
        };

        mstNode.Cid = CidV1.ComputeCidForDagCbor(mstNode.ToDagCborObject());


        //
        // Create repo commit.
        // First create unsigned, then sign it.
        //
        var repoCommit = new DbRepoCommit();
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
        var repoHeader = new DbRepoHeader
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
}