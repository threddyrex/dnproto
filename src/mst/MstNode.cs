using System.Security.Cryptography;
using System.Text;
using dnproto.repo;

namespace dnproto.mst;

/// <summary>
/// Represents a node in the Merkle Search Tree (MST).
/// 
/// An MST is a content-addressed tree structure that stores key/value mappings
/// in sorted order. Each node contains entries and optional links to sub-trees.
/// 
/// Mapping to firehose log format:
/// - "l" (left): LeftCid - link to sub-tree with keys sorting before all entries in this node
/// - "e" (entries): Entries - ordered list of MstEntry objects
/// 
/// The MST uses SHA-256 hashing with 2-bit chunks to determine key depth (fanout of 4).
/// Keys are compressed using prefix compression within each node.
/// </summary>
public class MstNode
{
    /// <summary>
    /// Optional CID link to a sub-tree node at a lower level.
    /// Contains all keys that sort before the first entry in this node.
    /// Maps to "l" in the MST structure (can be null).
    /// </summary>
    public CidV1? LeftCid { get; set; }

    /// <summary>
    /// Ordered list of entries in this node.
    /// Entries are sorted by key and use prefix compression.
    /// Maps to "e" in the MST structure.
    /// </summary>
    public List<MstEntry> Entries { get; set; } = new List<MstEntry>();

    /// <summary>
    /// The CID of this node (computed when serialized to DAG-CBOR).
    /// </summary>
    public CidV1? Cid { get; set; }

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
    /// Calculate the depth of a key (string version).
    /// Converts string to UTF-8 bytes first.
    /// </summary>
    public static int GetKeyDepth(string key)
    {
        return GetKeyDepth(Encoding.UTF8.GetBytes(key));
    }

    /// <summary>
    /// Serialize this MST node to DAG-CBOR format and return the bytes.
    /// The structure matches the spec:
    /// - "l": left CID (nullable)
    /// - "e": array of entries, each with "p", "k", "v", "t"
    /// </summary>
    public byte[] ToDagCbor()
    {
        using var ms = new MemoryStream();
        
        // Create the node object
        var nodeDict = new Dictionary<string, DagCborObject>();

        // Add left link if present
        if (LeftCid != null)
        {
            nodeDict["l"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = LeftCid
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
            var entryDict = new Dictionary<string, DagCborObject>();

            // "p" - prefix length
            entryDict["p"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = entry.PrefixLength
            };

            // "k" - key suffix (byte string)
            entryDict["k"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_BYTE_STRING, AdditionalInfo = 0, OriginalByte = 0 },
                Value = entry.KeySuffix
            };

            // "v" - value CID
            entryDict["v"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = entry.ValueCid
            };

            // "t" - tree CID (nullable)
            if (entry.TreeCid != null)
            {
                entryDict["t"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                    Value = entry.TreeCid
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

            entriesArray.Add(new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
                Value = entryDict
            });
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

        DagCborObject.WriteToStream(nodeObj, ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Deserialize an MST node from DAG-CBOR bytes.
    /// </summary>
    public static MstNode FromDagCbor(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return FromDagCbor(ms);
    }

    /// <summary>
    /// Deserialize an MST node from a stream containing DAG-CBOR data.
    /// </summary>
    public static MstNode FromDagCbor(Stream stream)
    {
        var obj = DagCborObject.ReadFromStream(stream);
        return FromDagCborObject(obj);
    }

    /// <summary>
    /// Convert a DagCborObject to an MstNode.
    /// </summary>
    public static MstNode FromDagCborObject(DagCborObject obj)
    {
        var node = new MstNode();

        if (obj.Value is not Dictionary<string, DagCborObject> dict)
        {
            throw new Exception("Expected MST node to be a map");
        }

        // MST nodes MUST have an "e" (entries) field - this distinguishes them from regular records
        if (!dict.TryGetValue("e", out var entriesObj))
        {
            throw new Exception("Not an MST node - missing 'e' (entries) field");
        }

        // Parse "l" (left link)
        if (dict.TryGetValue("l", out var leftObj))
        {
            if (leftObj.Value is CidV1 leftCid)
            {
                node.LeftCid = leftCid;
            }
            // else null
        }

        // Parse "e" (entries)
        if (entriesObj.Value is List<DagCborObject> entriesList)
        {
            foreach (var entryObj in entriesList)
            {
                if (entryObj.Value is not Dictionary<string, DagCborObject> entryDict)
                {
                    continue;
                }

                var entry = new MstEntry();

                // "p" - prefix length
                if (entryDict.TryGetValue("p", out var pObj) && pObj.Value is int p)
                {
                    entry.PrefixLength = p;
                }

                // "k" - key suffix
                if (entryDict.TryGetValue("k", out var kObj) && kObj.Value is byte[] k)
                {
                    entry.KeySuffix = k;
                }

                // "v" - value CID
                if (entryDict.TryGetValue("v", out var vObj) && vObj.Value is CidV1 v)
                {
                    entry.ValueCid = v;
                }

                // "t" - tree CID (nullable)
                if (entryDict.TryGetValue("t", out var tObj) && tObj.Value is CidV1 t)
                {
                    entry.TreeCid = t;
                }

                node.Entries.Add(entry);
            }
        }

        return node;
    }

    /// <summary>
    /// Compute the CID for this node.
    /// </summary>
    public CidV1 ComputeCid()
    {
        var bytes = ToDagCbor();
        var hash = SHA256.HashData(bytes);

        // Create CIDv1 with dag-cbor multicodec (0x71) and sha256 (0x12)
        var cid = new CidV1
        {
            Version = new VarInt { Value = 1 },
            Multicodec = new VarInt { Value = 0x71 },
            HashFunction = new VarInt { Value = 0x12 },
            DigestSize = new VarInt { Value = 32 },
            DigestBytes = hash,
            AllBytes = Array.Empty<byte>(), // Will be set below
            Base32 = ""
        };

        using var ms = new MemoryStream();
        CidV1.WriteCid(ms, cid);
        cid.AllBytes = ms.ToArray();
        cid.Base32 = "b" + Base32Encoding.BytesToBase32(cid.AllBytes);

        this.Cid = cid;
        return cid;
    }
}
