using System.Security.Cryptography;
using System.Text;
using dnproto.pds.db;
using dnproto.repo;

namespace dnproto.pds;


/// <summary>
/// MST logic layered on top of PdsDb.
/// </summary>
public class Mst
{
    private PdsDb _db;

    private Dictionary<CidV1, MstNode>? _mstNodeCache = null;
    private Dictionary<CidV1, List<MstEntry>>? _mstEntryCache = null;

    public Mst(PdsDb db)
    {
        _db = db;

        _mstNodeCache = _db.GetAllMstNodes().ToDictionary(n => n.Cid!);
        _mstEntryCache = GetMstEntriesByNode();
    }

    public List<MstNode> GetAllMstNodes()
    {
        return _mstNodeCache!.Values.ToList();
    }

    public Dictionary<CidV1, List<MstEntry>> GetAllMstEntriesByNode()
    {
        return _mstEntryCache!;
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
        List<MstEntry> mstEntries = _db.GetAllMstEntries();

        string? currentKey = null;

        foreach(var entry in mstEntries)
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
        foreach (MstEntry entry in _db.GetAllMstEntries())
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
    public int GetKeyDepth(string key)
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
    public int GetKeyDepth(byte[] key)
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




    /// <summary>
    /// Put an entry into the MST stored in the database.
    /// </summary>
    /// <param name="db"></param>
    /// <param name="key"></param>
    /// <param name="recordCid"></param>
    public void PutEntry(string key, CidV1 recordCid)
    {
        //
        // Load from db
        //
        var repoCommit = _db.GetRepoCommit();
        var mstNodeRoot = _mstNodeCache![repoCommit!.RootMstNodeCid!];

        InternalPutEntry(key, recordCid, mstNodeRoot, currentDepth: 0);
    }


    private void InternalPutEntry(string key, CidV1 recordCid, MstNode currentNode, int currentDepth)
    {
        int keyDepth = GetKeyDepth(key);
        string prefix = "";

        //
        // if we're at the right depth, insert here
        //
        if(keyDepth == currentDepth)
        {
            var entries = _mstEntryCache![currentNode.Cid!] ?? new List<MstEntry>();
            int insertIndex = 0;

            for(int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string fullKey = entry.GetFullKey(prefix);
                int comparison = CompareKeys(key, fullKey);

                // ✅
                if(comparison == 0)
                {
                    // Key already exists, update recordCid and then update db.
                    entry.RecordCid = recordCid;
                    _db.ReplaceMstEntriesForNode(currentNode.Cid, entries); // ➡️
                    return;
                }
                else if(comparison < 0)
                {
                    // Insert new entry before this one
                    insertIndex = i;
                    break;
                }

                prefix = fullKey;
                insertIndex++;
            }

            // calc pref len
            int prefixLength = 0;
            if (insertIndex > 0)
            {
                var prevEntry = entries[insertIndex - 1];
                string prevFullKey = prevEntry.GetFullKey(prefix);
                while (prefixLength < prevFullKey.Length && prefixLength < key.Length &&
                       prevFullKey[prefixLength] == key[prefixLength])
                {
                    prefixLength++;
                }
            }

            // create new entry
            var newEntry = new MstEntry
            {
                MstNodeCid = currentNode.Cid,
                EntryIndex = insertIndex,
                KeySuffix = key.Substring(prefixLength),
                PrefixLength = prefixLength,
                TreeMstNodeCid = null,
                RecordCid = recordCid
            };

            entries.Insert(insertIndex, newEntry);

            // TODO: stopped here
            // fix prefix lengths
            
            // fix entry indices

            // replce in db
        }
        //
        // otherwise, go to next level
        //
        else
        {
        }

        // TODO: stopped here
    }

    /// <summary>
    /// Compare two keys lexicographically.
    /// </summary>
    public int CompareKeys(string a, string b)
    {
        int minLen = Math.Min(a.Length, b.Length);
        for (int i = 0; i < minLen; i++)
        {
            if (a[i] != b[i])
            {
                return a[i] - b[i];
            }
        }
        return a.Length - b.Length;
    }

}