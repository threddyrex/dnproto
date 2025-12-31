using System.Security.Cryptography;
using System.Text;

namespace dnproto.repo;


/// <summary>
/// MST logic for a repo, including nodes and entries.
/// </summary>
public class Mst
{
    CidV1 _rootCid;
    List<MstNode> _mstNodes;
    List<MstEntry> _mstEntries;
    
    public Mst(CidV1 rootCid, List<MstNode> mstNodes, List<MstEntry> mstEntries)
    {
        _rootCid = rootCid;
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

    /// <summary>
    /// Calculate the depth of a key (string version).
    /// Converts string to UTF-8 bytes first.
    /// </summary>
    public static int GetKeyDepth(string key)
    {
        return GetKeyDepth(Encoding.UTF8.GetBytes(key));
    }



    /// <summary>
    /// Calculate the depth of a key using SHA-256 hash.
    /// 
    /// Per the spec:
    /// - Hash the key with SHA-256 (binary output)
    /// - Count leading zeros in 2-bit chunks
    /// - This gives a fanout of 4
    /// 
    /// Examples from spec:
    /// - "2653ae71" -> depth 0
    /// - "blue" -> depth 1
    /// - "app.bsky.feed.post/454397e440ec" -> depth 4
    /// - "app.bsky.feed.post/9adeb165882c" -> depth 8
    /// </summary>
    public static int GetKeyDepth(byte[] key)
    {
        // Hash the key with SHA-256
        byte[] hash = SHA256.HashData(key);

        // Count leading zeros in 2-bit chunks
        int leadingZeros = 0;
        foreach (byte b in hash)
        {
            if (b == 0)
            {
                leadingZeros += 8; // All 8 bits are zero
            }
            else
            {
                // Count leading zeros in this byte
                int mask = 0x80;
                for (int i = 0; i < 8; i++)
                {
                    if ((b & mask) == 0)
                    {
                        leadingZeros++;
                        mask >>= 1;
                    }
                    else
                    {
                        break;
                    }
                }
                break;
            }
        }

        // Divide by 2 to get 2-bit chunks
        return leadingZeros / 2;
    }



    public void Put(string key, DagCborObject recordObject)
    {
        //
        // Find the right node to put into
        //
    }
    
}