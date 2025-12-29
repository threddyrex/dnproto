

using dnproto.repo;

namespace dnproto.pds.db;

/// <summary>
/// MST entry
/// </summary>
public class DbMstEntry
{
    /// <summary>
    /// Cid for parent MST node. Helpful for lookups.
    /// </summary>
    public CidV1? MstNodeCid { get; set; } = null;
    
    /// <summary>
    /// "k"
    /// The remainder of the key after the prefix has been removed.
    /// When combined with the prefix from previous entries, forms the complete key.
    /// Maps to "k" in the MST structure (base64 encoded in JSON).
    /// The full keys are stored as plain text string (ex: "app.bsky.actor.profile/self")
    /// </summary>
    public string? KeySuffix { get; set; } = null;

    /// <summary>
    /// "p"
    /// Count of bytes shared with the previous entry in the node.
    /// The first entry in a node must have PrefixLength = 0.
    /// Maps to "p" in the MST structure.
    /// </summary>
    public int PrefixLength { get; set; } = 0;

    /// <summary>
    /// "t"
    /// Optional CID link to a sub-tree node at a lower level.
    /// Sub-tree contains keys that sort after this entry's key but before the next entry.
    /// Maps to "t" in the MST structure (can be null).
    /// </summary>
    public CidV1? TreeMstNodeCid { get; set; } = null;

    /// <summary>
    /// "v"
    /// CID link to the record data (DAG-CBOR object).
    /// Maps to "v" in the MST structure.
    /// </summary>
    public CidV1? RecordCid { get; set; } = null;


    public DagCborObject? ToDagCborObject()
    {
        if(RecordCid == null)
            return null;
    
        var entryDict = new Dictionary<string, DagCborObject>();

        // "p" - prefix length
        entryDict["p"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = PrefixLength
        };

        // "k" - key suffix (byte string)
        entryDict["k"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_BYTE_STRING, AdditionalInfo = 0, OriginalByte = 0 },
            Value = System.Text.Encoding.UTF8.GetBytes(KeySuffix ?? string.Empty)
        };

        // "v" - value CID
        entryDict["v"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
            Value = RecordCid
        };

        // "t" - tree CID (nullable)
        if (TreeMstNodeCid != null)
        {
            entryDict["t"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = TreeMstNodeCid
            };
        }
        else
        {
            entryDict["t"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_SIMPLE_VALUE, AdditionalInfo = 0x16, OriginalByte = 0 },
                Value = "null"
            };
        }

        return new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = entryDict
        };
    }
}