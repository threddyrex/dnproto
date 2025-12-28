

namespace dnproto.pds.db;

/// <summary>
/// MST node
/// </summary>
public class MstNode
{
    /// <summary>
    /// Cid for this node.
    /// Base 32, starting with "b".
    /// </summary>
    public required string Cid { get; set; }

    /// <summary>
    /// Optional CID link to a sub-tree node at a lower level.
    /// Contains all keys that sort before the first entry in this node.
    /// Maps to "l" in the MST structure (can be null).
    /// </summary>
    public string? LeftMstNodeCid { get; set; } = null;

}