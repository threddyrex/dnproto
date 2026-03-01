
namespace dnproto.repo;

public class Repo
{
    ///
    /// A repo has the following structure.
    /// 
    ///     RepoHeader.cs (only one (1))
    ///         "roots" -> CidV1 RepoCommitCid (points to RepoCommit.cs)
    ///         "version" -> Int Version
    ///
    ///     RepoCommit.cs (only one (1))
    ///         CidV1 Cid;
    ///         "did" -> string Did (user's did)
    ///         "rev" -> string Rev (increases monotonically, typically timestamp)
    ///         "sig" -> string Signature (computed each time repo changes, from the private key)
    ///         "data" -> CidV1 RootMstNodeCid (points to root MstNode cid)
    ///         "prev" -> CidV1? PrevMstNodeCid (usually null)
    ///         "version" -> int Version (always 3)
    ///
    ///     MstNode.cs (many (n))
    ///         CidV1 Cid
    ///         int KeyDepth (depth of this node in the tree)
    ///         "e" -> List<MstEntry> Entries
    ///         "l" -> MstNode? LeftTree (optional to a sub-tree MstNode)
    ///
    ///     MstEntry.cs (many (n))
    ///         string Key (the full key, built from the collection, suffix, and prefix length)
    ///         string Value (the cid of the repo record, in base32)
    ///         "k" -> (key suffix)
    ///         "p" -> (prefix length)
    ///         "t" -> MstNode? RightTree (optional to a sub-tree MstNode)
    ///         "v" -> (cid of repo record, see "Value" above)
    ///
    ///     RepoRecord.cs (many (n))
    ///         CidV1 Cid
    ///         DagCborObject Data (the actual atproto record)
    /// 
    /// 
    /// (Notes)
    ///     The three Repo* classes are in the "dnproto.repo" namespace. 
    ///     The two Mst* classes are in the "dnproto.mst" namespace. 
    ///     The "dnproto.mst" namespace is somewhat generic, so RepoMst.cs exists to bridge that gap.
    ///     Items in quotes are the field names in the dag-cbor objects.
    /// 




    /// <summary>
    /// 
    /// WalkRepo
    /// 
    /// Start at beginning and read through entire repo. 
    /// Use callbacks to let caller know when we find things.
    /// This is just the top-level algorithm. Most of the heavy lifting
    /// is done in VarInt, CidV1, and DagCborObject.
    /// 
    /// https://ipld.io/specs/transport/car/carv1/
    /// https://atproto.com/specs/repository
    /// https://ipld.io/specs/codecs/dag-cbor/spec/
    /// 
    ///
    /// Format from spec:
    /// 
    ///    [---  header  -------- ]   [----------------- data ---------------------------------]
    ///    [varint | header block ]   [varint | cid | data block]....[varint | cid | data block] 
    ///
    /// 
    /// Represented using the data types we have:
    /// 
    ///    [---  header  -------- ]   [----------------- data -------------------------------------------]
    ///    [VarInt | DagCborObject]   [VarInt | CidV1 | DagCborObject]....[VarInt | CidV1 | DagCborObject] 
    ///
    ///
    /// </summary>
    /// <param name="s"></param>
    /// <param name="headerCallback"></param>   
    /// <param name="recordCallback"></param>
    public static void WalkRepo(Stream s, Func<RepoHeader, bool> headerCallback, Func<RepoRecord, bool> recordCallback)
    {
        if (s == null) return;
        if (s.Length == 0) return;

        // Read header
        var repoHeader = RepoHeader.ReadFromStream(s);
        bool keepGoing = headerCallback(repoHeader);

        while (s.Position < s.Length && keepGoing)
        {
            // Read data block (record)
            // (this could wind up being either a MstNode, RepoCommit, or atproto RepoRecord)
            var repoRecord = RepoRecord.ReadFromStream(s);
            keepGoing = recordCallback(repoRecord);
        }
    }


    public static void WalkRepo(string repoFile, Func<RepoHeader, bool> headerCallback, Func<RepoRecord, bool> recordCallback)
    {
        if (string.IsNullOrEmpty(repoFile)) return;
        using (var fs = new FileStream(repoFile, FileMode.Open))
        {
            WalkRepo(fs, headerCallback, recordCallback);
        }
    }
}