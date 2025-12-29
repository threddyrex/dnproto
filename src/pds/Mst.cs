
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
        String Cid
        "l" - String? LeftMstNodeCid

    ✅ DbMstEntry (0 or more)
        String MstNodeCid
        "k" - String KeySuffix (plaintext - we can convert to base64 later)
        "p" - Int PrefixLength
        "t" - String? TreeMstNodeCid
        "v" - String RecordCid (cid of atproto record)

    ✅ DbRepoRecord (0 or more)
        String Cid
        String Data

*/


public class Mst
{
    
}