

using System.Security.Cryptography;
using System.Text;


namespace dnproto.repo;

/// <summary>
/// MST entry
/// </summary>
public class MstEntry
{
    /// <summary>
    /// 
    /// "k"
    /// 
    /// The remainder of the key after the prefix has been removed.
    /// When combined with the prefix from previous entries, forms the complete key.
    /// Maps to "k" in the MST structure (base64 encoded in JSON).
    /// The full keys are stored as plain text string (ex: "app.bsky.actor.profile/self")
    /// </summary>
    public string? KeySuffix { get; set; } = null;

    /// <summary>
    /// 
    /// "p"
    /// 
    /// Count of bytes shared with the previous entry in the node.
    /// The first entry in a node must have PrefixLength = 0.
    /// Maps to "p" in the MST structure.
    /// </summary>
    public int PrefixLength { get; set; } = 0;

    /// <summary>
    /// 
    /// "t"
    /// 
    /// Optional CID link to a sub-tree node at a lower level.
    /// Sub-tree contains keys that sort after this entry's key but before the next entry.
    /// Maps to "t" in the MST structure (can be null).
    /// </summary>
    public CidV1? TreeMstNodeCid { get; set; } = null;

    /// <summary>
    /// 
    /// "v"
    /// 
    /// CID link to the record data (DAG-CBOR object).
    /// Maps to "v" in the MST structure.
    /// </summary>
    public CidV1? RecordCid { get; set; } = null;


    /// <summary>
    /// Index of this entry within the MST node's entries list.
    /// </summary>
    public int EntryIndex { get; set; } = 0;


    /// <summary>
    /// NodeObjectId
    /// 
    /// This is not part of AT Proto. It is an internal database object id
    /// used when making edits to the tree. It's difficult to use Cid for this,
    /// because Cids change when the node is modified.
    /// </summary>
    public Guid? NodeObjectId { get; set; } = null;



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

    public static MstEntry? FromDagCborObject(DagCborObject? obj)
    {
        if (obj == null || obj.Type.MajorType != DagCborType.TYPE_MAP)
            return null;

        var entry = new MstEntry();

        // "p" - prefix length
        entry.PrefixLength = obj.SelectInt(new[] { "p" }) ?? 0;

        // "k" - key suffix
        var keyBytes = (byte[]?)obj.SelectObjectValue(new[] { "k" });
        entry.KeySuffix = keyBytes != null ? System.Text.Encoding.UTF8.GetString(keyBytes) : null;

        // "v" - record CID
        entry.RecordCid = (CidV1?)obj.SelectObjectValue(new[] { "v" });

        // "t" - tree CID (nullable)
        var treeNodeCid = obj.SelectObjectValue(new[] { "t" });
        if(treeNodeCid != null && treeNodeCid is CidV1)
        {
            entry.TreeMstNodeCid = (CidV1)treeNodeCid;
        }

        return entry;
    }


    /// <summary>
    /// Get full key for this entry given the previous full key
    /// </summary>
    /// <param name="previousKey"></param>
    /// <returns></returns>
    public string GetFullKey(string? previousKey)
    {
        if(EntryIndex == 0 || previousKey == null)
        {
            return KeySuffix ?? string.Empty;
        }
        else
        {
            return previousKey.Substring(0, PrefixLength) + (KeySuffix ?? string.Empty);
        }
    }


    #region STATIC

    /// <summary>
    /// Get full keys for a list of entries
    /// </summary>
    /// <param name="entries"></param>
    /// <returns></returns>
    public static List<string> GetFullKeys(List<MstEntry> entries)
    {
        var fullKeys = new List<string>();
        string? previousFullKey = null;

        for(int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            if(i == 0)
            {
                previousFullKey = entry.KeySuffix;
                fullKeys.Add(previousFullKey!);
                continue;
            }
            else
            {
                string fullKey = entry.GetFullKey(previousFullKey);
                fullKeys.Add(fullKey);
                previousFullKey = fullKey;
                continue;
            }
        }

        return fullKeys;
    }




    /// <summary>
    /// Loop through list of entries and fix their EntryIndex values (0 to n-1).
    /// </summary>
    /// <param name="entries"></param>
    public static void FixEntryIndexes(List<MstEntry> entries)
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
    public static void FixPrefixLengths(List<MstEntry> entries)
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
    public static int GetCommonPrefixLength(string a, string b)
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



    /// <summary>
    /// Compare two keys lexicographically.
    /// </summary>
    public static int CompareKeys(string a, string b)
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


    #endregion


}