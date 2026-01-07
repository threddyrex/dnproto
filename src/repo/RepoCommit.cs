


namespace dnproto.repo;

/// <summary>
/// Repo Commit for the repo. Can be only one.
/// </summary>
public class RepoCommit
{
    public string? Did { get; set; } = null;

    public int Version { get; set; } = 3;

    /// <summary>
    /// Cid of this commit object.
    /// Base 32, starting with "b".
    /// </summary>
    public CidV1? Cid { get; set; }

    /// <summary>
    /// Points to the cid of the root MST node for the repo.
    /// Base 32, starting with "b".
    /// </summary>
    public CidV1? RootMstNodeCid { get; set; }

    /// <summary>
    /// Revision string for this commit.
    /// Increases monotonically. Typically a timestamp-based string.
    /// </summary>
    public string? Rev { get; set; } = null;

    /// <summary>
    /// Points to the cid of the previous commit for the repo.
    /// Base 32, starting with "b".
    /// Usually null.
    /// </summary>
    public CidV1? PrevMstNodeCid { get; set; }

    /// <summary>
    /// Signature of this commit.
    /// Base 64.
    /// </summary>
    public byte[]? Signature { get; set; }

    public static bool IsRepoCommit(DagCborObject? obj)
    {
        bool notNull = obj != null;
        bool isMap = obj?.Type.MajorType == DagCborType.TYPE_MAP;
        bool containsSig = obj?.SelectObjectValue(new[]{"sig"}) != null;
        bool containsRev = (obj?.SelectObjectValue(new[]{"rev"}) as string) != null;
        bool containsDid = (obj?.SelectObjectValue(new[]{"did"}) as string) != null;
        return notNull && isMap && containsSig && containsRev;
    }


    public byte[]? ToDagCborBytes()
    {
        var dagCborObject = ToDagCborObject();
        if (dagCborObject == null)
            return null;

        using var ms = new MemoryStream();
        DagCborObject.WriteToStream(dagCborObject, ms);
        return ms.ToArray();
    }

    public DagCborObject? ToDagCborObject()
    {
        if (Did == null || RootMstNodeCid == null || Rev == null)
            return null;

        using var ms = new MemoryStream();
        var commitDict = new Dictionary<string, DagCborObject>();

        // All fields from unsigned commit
        commitDict["did"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Did
        };

        commitDict["version"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Version
        };

        if (RootMstNodeCid != null)
        {
            commitDict["data"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = RootMstNodeCid
            };
        }

        commitDict["rev"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Rev
        };

        if (PrevMstNodeCid != null)
        {
            commitDict["prev"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = PrevMstNodeCid
            };
        }
        else
        {
            commitDict["prev"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_SIMPLE_VALUE, AdditionalInfo = 0x16, OriginalByte = 0 },
                Value = "null"
            };
        }

        if(Signature != null && Signature.Length > 0)
        {
            // "sig" - signature bytes
            commitDict["sig"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_BYTE_STRING, AdditionalInfo = 0, OriginalByte = 0 },
                Value = Signature
            };            
        }

        var commitObj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = commitDict
        };

        return commitObj;
    }

    public static RepoCommit? FromDagCborObject(DagCborObject? obj)
    {
        if (!IsRepoCommit(obj))
            return null;

        var repoCommit = new RepoCommit();

        // did
        repoCommit.Did = obj?.SelectObjectValue(new[] { "did" }) as string;

        // version
        var versionObj = obj?.SelectObjectValue(new[] { "version" }) as int?;
        repoCommit.Version = versionObj ?? 3;

        // data (root MST node cid)
        var dataObj = obj?.SelectObjectValue(new[] { "data" }) as CidV1;
        repoCommit.RootMstNodeCid = dataObj;

        // rev
        var revObj = obj?.SelectObjectValue(new[] { "rev" }) as string;
        repoCommit.Rev = revObj;

        // prev
        var prevObj = obj?.SelectObjectValue(new[] { "prev" }) as CidV1;
        repoCommit.PrevMstNodeCid = prevObj;

        // sig
        var sigObj = obj?.SelectObjectValue(new[] { "sig" }) as byte[];
        repoCommit.Signature = sigObj;

        return repoCommit;
    }



    public void SignAndRecomputeCid(CidV1 newRootMstNodeCid, Func<byte[], byte[]> commitSigningFunction)
    {
        this.PrevMstNodeCid = this.Cid;
        this.RootMstNodeCid = newRootMstNodeCid;
        this.Rev = RecordKey.GenerateTid();

        //
        // Sign the commit
        //
        byte[]? unsignedBytes = this.ToDagCborBytes()!;
        var hash = System.Security.Cryptography.SHA256.HashData(unsignedBytes);        
        this.Signature = commitSigningFunction(hash);
        byte[]? repoCommitObjSignedBytes = this.ToDagCborBytes();
        this.Cid = CidV1.ComputeCidForDagCbor(this.ToDagCborObject()!);
    }

}