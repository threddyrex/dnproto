


using Microsoft.AspNetCore.Authentication;

namespace dnproto.repo;

/// <summary>
/// Repo Commit for the repo. Can be only one.
/// </summary>
public class RepoCommit
{
    public required string Did;

    public required int Version;

    /// <summary>
    /// 
    /// "data"
    /// 
    /// Points to the cid of the root MST node for the repo.
    /// Base 32, starting with "b".
    /// </summary>
    public required CidV1 RootMstNodeCid;

    /// <summary>
    /// 
    /// "rev"
    /// 
    /// Revision string for this commit.
    /// Increases monotonically. Typically a timestamp-based string.
    /// </summary>
    public required string Rev;

    /// <summary>
    /// Points to the cid of the previous commit for the repo.
    /// Base 32, starting with "b".
    /// Usually null.
    /// </summary>
    public CidV1? PrevMstNodeCid { get; set; }

    /// <summary>
    /// Cid of this commit object.
    /// Base 32, starting with "b".
    /// </summary>
    public CidV1? Cid { get; set; }


    /// <summary>
    /// 
    /// "sig"
    /// 
    /// Signature of this commit.
    /// Base 64.
    /// </summary>
    public byte[]? Signature { get; set; }

    public static bool IsRepoCommit(DagCborObject? obj)
    {
        bool notNull = obj != null;
        bool isMap = obj?.Type.MajorType == DagCborType.TYPE_MAP;
        bool containsVersion = obj?.SelectObjectValue(new[]{"version"}) != null;
        bool containsRev = (obj?.SelectObjectValue(new[]{"rev"}) as string) != null;
        bool containsDid = (obj?.SelectObjectValue(new[]{"did"}) as string) != null;
        return notNull && isMap && containsVersion && containsRev && containsDid;
    }


    #region DAG-CBOR

    public byte[] ToDagCborBytes()
    {
        var dagCborObject = ToDagCborObject();

        using var ms = new MemoryStream();
        DagCborObject.WriteToStream(dagCborObject, ms);
        return ms.ToArray();
    }

    public DagCborObject ToDagCborObject()
    {
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

    public static RepoCommit FromDagCborObject(CidV1 cid, DagCborObject obj)
    {
        //
        // Get values from the DagCborObject
        //
        string? did = obj?.SelectObjectValue(new[] { "did" }) as string;
        int? version = obj?.SelectObjectValue(new[] { "version" }) as int?;
        CidV1? data = obj?.SelectObjectValue(new[] { "data" }) as CidV1;
        string? rev = obj?.SelectObjectValue(new[] { "rev" }) as string;
        CidV1? prev = obj?.SelectObjectValue(new[] { "prev" }) as CidV1;
        byte[]? sig = obj?.SelectObjectValue(new[] { "sig" }) as byte[];


        //
        // Validate
        //
        if (did == null || version == null || data == null || rev == null)
        {
            throw new Exception("Invalid RepoCommit object - missing required fields.");
        }

        //
        // Return
        //
        return new RepoCommit
        {
            Cid = cid,
            Did = did,
            Version = version.Value,
            RootMstNodeCid = data,
            Rev = rev,
            PrevMstNodeCid = prev,
            Signature = sig
        };

    }

    #endregion


    #region SIGN

    public void SignAndRecomputeCid(CidV1 newRootMstNodeCid, Func<byte[], byte[]> commitSigningFunction)
    {
        //
        // Update fields (clear out signature and cid)
        //
        this.RootMstNodeCid = newRootMstNodeCid;
        this.Rev = RecordKey.GenerateTid();
        this.Signature = null;
        this.Cid = null;
        this.PrevMstNodeCid = null;

        //
        // Sign commit (w/o cid, signature)
        //
        byte[]? unsignedBytes = this.ToDagCborBytes()!;
        var hash = System.Security.Cryptography.SHA256.HashData(unsignedBytes);        
        this.Signature = commitSigningFunction(hash);

        //
        // Recompute Cid with Signature
        //
        byte[]? repoCommitObjSignedBytes = this.ToDagCborBytes();
        this.Cid = CidV1.ComputeCidForDagCbor(this.ToDagCborObject()!);
    }

    #endregion
}