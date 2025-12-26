using System.Text;
using dnproto.repo;

namespace dnproto.mst;

/// <summary>
/// Represents a signed commit object in an AT Protocol repository.
/// 
/// Commits are the top-level objects that point to MST roots and contain
/// cryptographic signatures. Each mutation of a repository creates a new commit.
/// 
/// Commit structure per spec (version 3):
/// - did: account DID (required)
/// - version: fixed value of 3 (required)
/// - data: CID link to MST root (required)
/// - rev: revision TID, monotonically increasing (required)
/// - prev: CID link to previous commit (nullable, usually null in v3)
/// - sig: cryptographic signature (required)
/// </summary>
public class RepoCommit
{
    /// <summary>
    /// The DID of the account that owns this repository.
    /// Must be in strictly normalized form (lowercase).
    /// </summary>
    public string Did { get; set; } = "";

    /// <summary>
    /// Repository format version. Fixed value of 3 for current spec.
    /// </summary>
    public int Version { get; set; } = 3;

    /// <summary>
    /// CID link to the root of the MST.
    /// This is what gets updated when records change.
    /// </summary>
    public CidV1? DataCid { get; set; }

    /// <summary>
    /// Revision identifier (TID format).
    /// Must increase monotonically. Typically uses current timestamp.
    /// </summary>
    public string Rev { get; set; } = "";

    /// <summary>
    /// CID link to the previous commit (for history chain).
    /// In v3, this is virtually always null but must exist in CBOR.
    /// </summary>
    public CidV1? PrevCid { get; set; }

    /// <summary>
    /// Cryptographic signature of the unsigned commit.
    /// Raw bytes of the signature.
    /// </summary>
    public byte[] Signature { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// The CID of this commit (computed after serialization).
    /// </summary>
    public CidV1? CommitCid { get; set; }

    /// <summary>
    /// Serialize the unsigned commit to DAG-CBOR.
    /// This is what gets hashed and signed.
    /// </summary>
    public byte[] ToUnsignedDagCbor()
    {
        using var ms = new MemoryStream();
        
        var commitDict = new Dictionary<string, DagCborObject>();

        // "did" - string
        commitDict["did"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Did
        };

        // "version" - integer
        commitDict["version"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Version
        };

        // "data" - CID link to MST root
        if (DataCid != null)
        {
            commitDict["data"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = DataCid
            };
        }

        // "rev" - string (TID format)
        commitDict["rev"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Rev
        };

        // "prev" - CID link or null
        if (PrevCid != null)
        {
            commitDict["prev"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = PrevCid
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

        var commitObj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = commitDict
        };

        DagCborObject.WriteToStream(commitObj, ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Serialize the signed commit to DAG-CBOR.
    /// Includes the signature field.
    /// </summary>
    public byte[] ToSignedDagCbor()
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

        if (DataCid != null)
        {
            commitDict["data"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = DataCid
            };
        }

        commitDict["rev"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Rev
        };

        if (PrevCid != null)
        {
            commitDict["prev"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = PrevCid
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

        // "sig" - signature bytes
        commitDict["sig"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_BYTE_STRING, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Signature
        };

        var commitObj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = commitDict
        };

        DagCborObject.WriteToStream(commitObj, ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Deserialize a commit from DAG-CBOR bytes.
    /// </summary>
    public static RepoCommit FromDagCbor(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return FromDagCbor(ms);
    }

    /// <summary>
    /// Deserialize a commit from a stream.
    /// </summary>
    public static RepoCommit FromDagCbor(Stream stream)
    {
        var obj = DagCborObject.ReadFromStream(stream);
        return FromDagCborObject(obj);
    }

    /// <summary>
    /// Convert a DagCborObject to a RepoCommit.
    /// </summary>
    public static RepoCommit FromDagCborObject(DagCborObject obj)
    {
        var commit = new RepoCommit();

        if (obj.Value is not Dictionary<string, DagCborObject> dict)
        {
            throw new Exception("Expected commit to be a map");
        }

        // Parse fields
        if (dict.TryGetValue("did", out var didObj) && didObj.Value is string did)
        {
            commit.Did = did;
        }

        if (dict.TryGetValue("version", out var versionObj) && versionObj.Value is int version)
        {
            commit.Version = version;
        }

        if (dict.TryGetValue("data", out var dataObj) && dataObj.Value is CidV1 data)
        {
            commit.DataCid = data;
        }

        if (dict.TryGetValue("rev", out var revObj) && revObj.Value is string rev)
        {
            commit.Rev = rev;
        }

        if (dict.TryGetValue("prev", out var prevObj) && prevObj.Value is CidV1 prev)
        {
            commit.PrevCid = prev;
        }

        if (dict.TryGetValue("sig", out var sigObj) && sigObj.Value is byte[] sig)
        {
            commit.Signature = sig;
        }

        return commit;
    }

    /// <summary>
    /// Sign this commit with a signing function.
    /// The signing function receives the hash of the unsigned commit and returns a signature.
    /// </summary>
    public void Sign(Func<byte[], byte[]> signingFunction)
    {
        // Serialize unsigned commit
        var unsignedBytes = ToUnsignedDagCbor();
        
        // Hash with SHA-256
        var hash = System.Security.Cryptography.SHA256.HashData(unsignedBytes);
        
        // Sign the hash
        Signature = signingFunction(hash);
    }

    /// <summary>
    /// Compute the CID of this signed commit.
    /// </summary>
    public CidV1 ComputeCid()
    {
        var bytes = ToSignedDagCbor();
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);

        var cid = new CidV1
        {
            Version = new VarInt { Value = 1 },
            Multicodec = new VarInt { Value = 0x71 }, // dag-cbor
            HashFunction = new VarInt { Value = 0x12 }, // sha256
            DigestSize = new VarInt { Value = 32 },
            DigestBytes = hash,
            AllBytes = Array.Empty<byte>(),
            Base32 = ""
        };

        using var ms = new MemoryStream();
        CidV1.WriteCid(ms, cid);
        cid.AllBytes = ms.ToArray();
        cid.Base32 = "b" + Base32Encoding.BytesToBase32(cid.AllBytes);

        CommitCid = cid;
        return cid;
    }
}
