using System.Security.Cryptography;
using System.Text;
using dnproto.pds.db;
using dnproto.repo;

namespace dnproto.pds;


/// <summary>
/// MST implementation, backed by database.
/// </summary>
public class Mst
{
    private PdsDb _db;

    public Mst(PdsDb db)
    {
        _db = db;
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


    #region PUT

    /// <summary>
    /// Put an entry into the MST stored in the database.
    /// It recursively calls InternalPutEntry to find the right place to insert the entry.
    /// For the part of the tree where an update happens, all of the cids from that location 
    /// all the way to the root *will be* recalculated. 
    /// (because a cid is a hash of the node and its entries).
    /// Db updates happen inside InternalPutEntry.
    /// </summary>
    /// <param name="db"></param>
    /// <param name="key"></param>
    /// <param name="recordCid"></param>
    public CidV1 PutEntry(string key, CidV1 recordCid)
    {
        //
        // Load from db
        //
        var repoCommit = _db.GetRepoCommit();
        var mstNodeRoot = _db.GetMstNode(repoCommit!.RootMstNodeCid!);

        var newCid = PutEntryAtNode(key, recordCid, mstNodeRoot!, currentDepth: 0);

        return newCid;
    }


    /// <summary>
    /// Internal recursive function to put an entry into the MST.
    /// Returns the cid for currentNode (because cids often change - they are a hash of the MST node).
    /// </summary>
    private CidV1 PutEntryAtNode(string key, CidV1 recordCid, MstNode currentNode, int currentDepth)
    {
        int keyDepth = GetKeyDepth(key);
        string prefix = "";
        var entries = _db.GetMstEntriesForNode(currentNode.Cid!);

        //
        // If depths are equal, we insert at *this* MstNode.
        //
        if(keyDepth == currentDepth)
        {
            int insertIndex = 0;

            //
            // Loop through entries to find insert position.
            //
            for(int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string fullKey = entry.GetFullKey(prefix);
                int comparison = CompareKeys(key, fullKey);

                //
                // Key exists already - update it.
                //
                if(comparison == 0)
                {
                    entry.RecordCid = recordCid;

                    //
                    // 🚨 DB UPDATE 🚨
                    //
                    CidV1 oldCid1 = currentNode.Cid!;
                    currentNode.Cid = CidV1.ComputeCidForDagCbor(currentNode.ToDagCborObject(entries))!;

                    _db.ReplaceMstNode(
                        oldCid1, // old cid
                        currentNode, // node
                        entries); // entries

                    return currentNode.Cid!;
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
                prefixLength = GetCommonPrefixLength(key, prevFullKey);
            }

            // create new entry
            var newEntry = new MstEntry
            {
                MstNodeCid = currentNode.Cid,
                EntryIndex = insertIndex, // gets fixed later
                KeySuffix = key.Substring(prefixLength), // gets fixed later
                PrefixLength = prefixLength, // gets fixed later
                TreeMstNodeCid = null,
                RecordCid = recordCid
            };

            entries.Insert(insertIndex, newEntry);

            // fix entry indices (0 -> n-1)
            FixEntryIndexes(entries);

            // fix prefix lengths
            FixPrefixLengths(entries);

            //
            // 🚨 DB UPDATE 🚨
            //
            CidV1 oldCid2 = currentNode.Cid!;
            currentNode.Cid = CidV1.ComputeCidForDagCbor(currentNode.ToDagCborObject(entries))!;

            _db.ReplaceMstNode(
                oldCid2, // old cid
                currentNode, // node
                entries); // entries

            return currentNode.Cid!;

        }
        //
        // Otherwise, we need to go to next level.
        // This could either mean:
        //      1 - the sub-tree of MstNode.LeftMstNodeCid, OR
        //      2 - the sub-tree of one of the entries' TreeMstNodeCid. 
        //
        else
        {
            int insertPos = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string entryKey = entry.GetFullKey(prefix);
                
                if (CompareKeys(key, entryKey) < 0)
                {
                    break;
                }
                
                prefix = entryKey;
                insertPos = i + 1;
            }
            /* TODO: stopped here
            if(insertPos == 0)
            {
                // Go to left subtree
                MstNode? leftNode = null;
                if (currentNode.LeftMstNodeCid != null)
                {
                    leftNode = _db.GetMstNode(currentNode.LeftMstNodeCid);
                }
                else
                {
                    // Create new left node
                    leftNode = new MstNode();
                    _db.InsertMstNode(leftNode);
                    currentNode.LeftMstNodeCid = leftNode.Cid;
                    _db.UpdateMstNode(currentNode);
                    _mstNodeCache![currentNode.Cid!] = currentNode;
                    _mstNodeCache![leftNode.Cid!] = leftNode;
                }

                // Recurse
                InternalPutEntry(key, recordCid, leftNode, currentDepth + 1);
            }
            else
            {
                // Go to right subtree of the selected entry
                var selectedEntry = entries[insertPos - 1];
                MstNode? rightNode = null;
                if (selectedEntry.TreeMstNodeCid != null)
                {
                    rightNode = _mstNodeCache![selectedEntry.TreeMstNodeCid];
                }
                else
                {
                    // Create new right node
                    rightNode = new MstNode();
                    _db.InsertMstNode(rightNode);
                    selectedEntry.TreeMstNodeCid = rightNode.Cid;
                    _db.UpdateMstEntry(selectedEntry);
                    _mstNodeCache![rightNode.Cid!] = rightNode;
                }

                // Recurse
                InternalPutEntry(key, recordCid, rightNode, currentDepth + 1);
            }*/
            return currentNode.Cid!;
        }
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


    /// <summary>
    /// Loop through list of entries and fix their EntryIndex values (0 to n-1).
    /// </summary>
    /// <param name="entries"></param>
    public void FixEntryIndexes(List<MstEntry> entries)
    {
        for(int i = 0; i < entries.Count; i++)
        {
            entries[i].EntryIndex = i;
        }
    }

    /// <summary>
    /// Fix the PrefixLength and KeySuffix values for the given entries.
    /// </summary>
    /// <param name="entries"></param>
    public void FixPrefixLengths(List<MstEntry> entries)
    {
        string previousFullKey = string.Empty;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (i == 0)
            {
                entry.PrefixLength = 0;
                previousFullKey = entry.KeySuffix!;
            }
            else
            {
                var entryFullKey = entry.GetFullKey(previousFullKey);
                int prefixLen = GetCommonPrefixLength(previousFullKey, entryFullKey);
                entry.PrefixLength = prefixLen;
                entry.KeySuffix = entryFullKey.Substring(prefixLen);
                previousFullKey = entryFullKey;
            }
        }
    }


    /// <summary>
    /// Get the length of the common prefix between two keys.
    /// </summary>
    public int GetCommonPrefixLength(string a, string b)
    {
        int len = 0;
        int minLen = Math.Min(a.Length, b.Length);
        
        for (int i = 0; i < minLen; i++)
        {
            if (a[i] == b[i])
            {
                len++;
            }
            else
            {
                break;
            }
        }
        
        return len;
    }




    #endregion


}