
namespace dnproto.repo;


/// <summary>
/// MST logic for a repo, including nodes and entries.
/// </summary>
public class Mst
{
    List<MstNode> _mstNodes;
    List<MstEntry> _mstEntries;
    
    public Mst(List<MstNode> mstNodes, List<MstEntry> mstEntries)
    {
        _mstNodes = mstNodes;
        _mstEntries = mstEntries;
    }



    /// <summary>
    /// Check if a key exists in the given MST nodes and entries.
    /// </summary>
    /// <param name="mstNodes"></param>
    /// <param name="mstEntries"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool KeyExists(string key)
    {
        string? currentKey = null;

        foreach(var entry in _mstEntries)
        {
            if(entry.EntryIndex == 0)
            {
                currentKey = entry.KeySuffix;
            }
            else
            {
                currentKey = currentKey!.Substring(0, entry.PrefixLength) + entry.KeySuffix;
            }

            if(currentKey == key)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get all MST entries grouped by their parent MST node Cid.
    /// </summary>
    /// <returns></returns>
    public Dictionary<CidV1, List<MstEntry>> GetMstEntriesByNode()
    {
        Dictionary<CidV1, List<MstEntry>> allMstEntriesByNode = new Dictionary<CidV1, List<MstEntry>>();
        foreach (MstEntry entry in _mstEntries)
        {
            if (entry.MstNodeCid == null)
                continue;

            if (!allMstEntriesByNode.ContainsKey(entry.MstNodeCid))
            {
                allMstEntriesByNode[entry.MstNodeCid] = new List<MstEntry>();
            }
            allMstEntriesByNode[entry.MstNodeCid].Add(entry);
        }

        return allMstEntriesByNode;

    }
    
}