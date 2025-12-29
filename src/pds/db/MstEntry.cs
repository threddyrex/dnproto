

namespace dnproto.pds.db;

/// <summary>
/// MST entry
/// </summary>
public class MstEntry
{
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
    public string? TreeMstNodeCid { get; set; } = null;

    /// <summary>
    /// "v"
    /// CID link to the record data (DAG-CBOR object).
    /// Maps to "v" in the MST structure.
    /// </summary>
    public string? RecordCid { get; set; } = null;
}