


namespace dnproto.repo;

/// <summary>
/// MST node. A node will have 0 or more entries. See MstEntry.
/// </summary>
public class MstNode
{
    /// <summary>
    /// NodeObjectId
    /// 
    /// This is not part of AT Proto. It is an internal database object id
    /// used when making edits to the tree. It's difficult to use Cid for this,
    /// because Cids change when the node is modified.
    /// </summary>
    public Guid? NodeObjectId { get; set; } = null;

    /// <summary>
    /// Cid for this node.
    /// Base 32, starting with "b".
    /// </summary>
    public CidV1? Cid { get; set; } = null;

    /// <summary>
    /// 
    /// "l"
    /// 
    /// Optional CID link to a sub-tree node at a lower level.
    /// Contains all keys that sort before the first entry in this node.
    /// Maps to "l" in the MST structure (can be null).
    /// </summary>
    public CidV1? LeftMstNodeCid { get; set; } = null;


    public static bool IsMstNode(DagCborObject? obj)
    {
        bool notNull = obj != null;
        bool isMap = obj?.Type.MajorType == DagCborType.TYPE_MAP;
        bool containsE = (obj?.SelectObjectValue(new[]{"e"}) as List<DagCborObject>) != null;
        return notNull && isMap && containsE;        
    }

    public byte[] ToDagCborBytes(List<MstEntry> entries)
    {
        var dagCborObject = ToDagCborObject(entries);
        using var ms = new MemoryStream();
        DagCborObject.WriteToStream(dagCborObject, ms);
        return ms.ToArray();
    }

    public DagCborObject ToDagCborObject(List<MstEntry> entries)
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
        foreach (var entry in entries)
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


    public static (MstNode?, List<MstEntry>?) FromDagCborObject(DagCborObject? obj)
    {
        if (obj == null || obj.Type.MajorType != DagCborType.TYPE_MAP)
            return (null, null);

        var node = new MstNode();
        var entries = new List<MstEntry>();

        // Left link
        var leftCid = obj.SelectObjectValue(new[] { "l" });
        if(leftCid is CidV1 cid)
        {
            node.LeftMstNodeCid = cid;
        }

        // Entries
        var entriesObj = (List<DagCborObject>?)obj.SelectObjectValue(new []{"e"});
        if (entriesObj != null)
        {
            foreach(var entryObject in entriesObj)
            {
                entries.Add(MstEntry.FromDagCborObject(entryObject)!);
            }
        }

        return (node, entries);
    }


    public void RecomputeCid(List<MstEntry> entries)
    {
        this.Cid = CidV1.ComputeCidForDagCbor(this.ToDagCborObject(entries))!;
    }
}