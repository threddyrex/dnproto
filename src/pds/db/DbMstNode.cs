

using dnproto.repo;

namespace dnproto.pds.db;

/// <summary>
/// MST node
/// </summary>
public class DbMstNode
{
    /// <summary>
    /// Cid for this node.
    /// Base 32, starting with "b".
    /// </summary>
    public CidV1? Cid { get; set; } = null;

    /// <summary>
    /// Optional CID link to a sub-tree node at a lower level.
    /// Contains all keys that sort before the first entry in this node.
    /// Maps to "l" in the MST structure (can be null).
    /// </summary>
    public CidV1? LeftMstNodeCid { get; set; } = null;

    /// <summary>
    /// Entries for this node.
    /// </summary>
    public List<DbMstEntry> Entries { get; set; } = new();


    public byte[] ToDagCborBytes()
    {
        var dagCborObject = ToDagCborObject();
        using var ms = new MemoryStream();
        DagCborObject.WriteToStream(dagCborObject, ms);
        return ms.ToArray();
    }

    public DagCborObject ToDagCborObject()
    {
        // Create the node object
        var nodeDict = new Dictionary<string, DagCborObject>();

        // Add left link if present
        if (LeftMstNodeCid != null)
        {
            nodeDict["l"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = LeftMstNodeCid
            };
        }
        else
        {
            nodeDict["l"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_SIMPLE_VALUE, AdditionalInfo = 0x16, OriginalByte = 0 },
                Value = "null"
            };
        }

        // Add entries array
        var entriesArray = new List<DagCborObject>();
        foreach (var entry in Entries)
        {
            var entryObj = entry.ToDagCborObject();
            if(entryObj != null)
            {
                entriesArray.Add(entryObj);
            }
        }

        nodeDict["e"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_ARRAY, AdditionalInfo = 0, OriginalByte = 0 },
            Value = entriesArray
        };

        // Serialize to CBOR
        var nodeObj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = nodeDict
        };

        return nodeObj;
    }
}