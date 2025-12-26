using dnproto.repo;

namespace dnproto.mst;

/// <summary>
/// Represents a single entry in an MST node.
/// 
/// Each entry contains a key (with prefix compression), a value (CID link to a record),
/// and optionally a link to a sub-tree.
/// 
/// Mapping to firehose log format:
/// - "p" (prefixlen): PrefixLength - count of bytes shared with previous entry
/// - "k" (keysuffix): KeySuffix - remainder of key after prefix removed (base64 encoded in JSON)
/// - "v" (value): ValueCid - CID link to the record data
/// - "t" (tree): TreeCid - CID link to sub-tree node (nullable)
/// </summary>
public class MstEntry
{
    /// <summary>
    /// Count of bytes shared with the previous entry in the node.
    /// The first entry in a node must have PrefixLength = 0.
    /// Maps to "p" in the MST structure.
    /// </summary>
    public int PrefixLength { get; set; }

    /// <summary>
    /// The remainder of the key after the prefix has been removed.
    /// When combined with the prefix from previous entries, forms the complete key.
    /// Maps to "k" in the MST structure (base64 encoded in JSON).
    /// </summary>
    public byte[] KeySuffix { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// CID link to the record data (DAG-CBOR object).
    /// Maps to "v" in the MST structure.
    /// </summary>
    public CidV1 ValueCid { get; set; } = null!;

    /// <summary>
    /// Optional CID link to a sub-tree node at a lower level.
    /// Sub-tree contains keys that sort after this entry's key but before the next entry.
    /// Maps to "t" in the MST structure (can be null).
    /// </summary>
    public CidV1? TreeCid { get; set; }

    /// <summary>
    /// Get the full key by combining prefix and suffix.
    /// Requires the previous entries to reconstruct the prefix.
    /// </summary>
    public byte[] GetFullKey(byte[] prefix)
    {
        byte[] fullKey = new byte[prefix.Length + KeySuffix.Length];
        Array.Copy(prefix, 0, fullKey, 0, prefix.Length);
        Array.Copy(KeySuffix, 0, fullKey, prefix.Length, KeySuffix.Length);
        return fullKey;
    }
}
