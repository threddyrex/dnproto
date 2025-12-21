using dnproto.sdk.repo;

namespace dnproto.sdk.mst;

/// <summary>
/// Represents a single operation in a firehose commit event.
/// 
/// Operations indicate what changed in a repository commit.
/// Maps to the "ops" array in firehose messages.
/// 
/// Example from log:
/// {
///   "cid": "bafyreiboh42hw2g7ebt6hqimqh2jaujaxoxsobfulwfefdemoqlgoxnhs4",
///   "path": "app.bsky.feed.post/3mafdjohym22f",
///   "action": "create"
/// }
/// </summary>
public class RepoOperation
{
    /// <summary>
    /// The action being performed: "create", "update", or "delete"
    /// </summary>
    public string Action { get; set; } = "";

    /// <summary>
    /// The repository path (collection/recordKey)
    /// Example: "app.bsky.feed.post/3kj1..."
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// CID of the record (for create/update).
    /// Null for delete operations.
    /// </summary>
    public CidV1? Cid { get; set; }

    /// <summary>
    /// Convert to DAG-CBOR object for serialization.
    /// </summary>
    public DagCborObject ToDagCborObject()
    {
        var dict = new Dictionary<string, DagCborObject>();

        dict["action"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Action
        };

        dict["path"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Path
        };

        if (Cid != null)
        {
            dict["cid"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = Cid
            };
        }

        return new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = dict
        };
    }
}

/// <summary>
/// Represents a firehose commit event.
/// 
/// This is the main message type emitted when a repository changes.
/// Maps to the firehose message body (second DAG-CBOR object).
/// 
/// Example from log:
/// {
///   "ops": [...],
///   "rev": "3mafdjpvybs2k",
///   "seq": 44001,
///   "repo": "did:plc:watmxkxfjbwyxfuutganopfk",
///   "time": "2025-12-20T04:10:21.264Z",
///   "blobs": [],
///   "since": "3mafdfsqtak2k",
///   "blocks": "...",  // base64 encoded CAR bytes
///   "commit": "bafyreigt64laorbr7477z5zykyljsmvzupkkhxw62sgwyrbuj4rxxwqeb4",
///   "rebase": false,
///   "tooBig": false,
///   "prevData": "bafyreicelfr5tisybu3cy2c2tkpilcwoltm7cna6eox4a7bruiflwrgjje"
/// }
/// </summary>
public class FirehoseCommitEvent
{
    /// <summary>
    /// List of operations (create/update/delete) in this commit.
    /// </summary>
    public List<RepoOperation> Ops { get; set; } = new List<RepoOperation>();

    /// <summary>
    /// Repository DID.
    /// </summary>
    public string Repo { get; set; } = "";

    /// <summary>
    /// Revision TID of this commit.
    /// </summary>
    public string Rev { get; set; } = "";

    /// <summary>
    /// Previous revision TID (before this commit).
    /// </summary>
    public string? Since { get; set; }

    /// <summary>
    /// Sequence number in the firehose stream.
    /// </summary>
    public long Seq { get; set; }

    /// <summary>
    /// Timestamp of the commit.
    /// </summary>
    public string Time { get; set; } = "";

    /// <summary>
    /// CID of the commit object.
    /// </summary>
    public CidV1? Commit { get; set; }

    /// <summary>
    /// CID of the previous MST root (data).
    /// Null for first commit.
    /// </summary>
    public CidV1? PrevData { get; set; }

    /// <summary>
    /// CAR file bytes containing the diff (blocks that changed).
    /// This is a base64-encoded byte array in JSON.
    /// </summary>
    public byte[] Blocks { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// List of blob CIDs referenced in this commit.
    /// </summary>
    public List<CidV1> Blobs { get; set; } = new List<CidV1>();

    /// <summary>
    /// Whether this is a rebase operation.
    /// </summary>
    public bool Rebase { get; set; }

    /// <summary>
    /// Whether the commit was too large to include all data.
    /// </summary>
    public bool TooBig { get; set; }

    /// <summary>
    /// Convert to DAG-CBOR object for serialization to firehose.
    /// </summary>
    public DagCborObject ToDagCborObject()
    {
        var dict = new Dictionary<string, DagCborObject>();

        // "ops" array
        var opsArray = new List<DagCborObject>();
        foreach (var op in Ops)
        {
            opsArray.Add(op.ToDagCborObject());
        }
        dict["ops"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_ARRAY, AdditionalInfo = 0, OriginalByte = 0 },
            Value = opsArray
        };

        // "repo"
        dict["repo"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Repo
        };

        // "rev"
        dict["rev"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Rev
        };

        // "since"
        if (Since != null)
        {
            dict["since"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = Since
            };
        }

        // "seq"
        dict["seq"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = (int)Seq
        };

        // "time"
        dict["time"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Time
        };

        // "commit"
        if (Commit != null)
        {
            dict["commit"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = Commit
            };
        }

        // "prevData"
        if (PrevData != null)
        {
            dict["prevData"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = PrevData
            };
        }

        // "blocks" (byte string)
        dict["blocks"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_BYTE_STRING, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Blocks
        };

        // "blobs" array
        var blobsArray = new List<DagCborObject>();
        foreach (var blob in Blobs)
        {
            blobsArray.Add(new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = blob
            });
        }
        dict["blobs"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_ARRAY, AdditionalInfo = 0, OriginalByte = 0 },
            Value = blobsArray
        };

        // "rebase"
        dict["rebase"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_SIMPLE_VALUE, AdditionalInfo = (byte)(Rebase ? 0x15 : 0x14), OriginalByte = 0 },
            Value = Rebase
        };

        // "tooBig"
        dict["tooBig"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_SIMPLE_VALUE, AdditionalInfo = (byte)(TooBig ? 0x15 : 0x14), OriginalByte = 0 },
            Value = TooBig
        };

        return new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = dict
        };
    }

    /// <summary>
    /// Serialize to bytes (for sending over WebSocket).
    /// Returns header + body concatenated.
    /// </summary>
    public byte[] ToFirehoseBytes()
    {
        using var ms = new MemoryStream();

        // Header: { "t": "#commit", "op": 1 }
        var headerDict = new Dictionary<string, DagCborObject>();
        headerDict["t"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = "#commit"
        };
        headerDict["op"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = 1
        };
        var headerObj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = headerDict
        };

        // Write header
        DagCborObject.WriteToStream(headerObj, ms);

        // Write body
        DagCborObject.WriteToStream(ToDagCborObject(), ms);

        return ms.ToArray();
    }
}
