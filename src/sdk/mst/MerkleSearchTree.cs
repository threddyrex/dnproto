using System.Security.Cryptography;
using System.Text;
using dnproto.sdk.repo;

namespace dnproto.sdk.mst;

/// <summary>
/// Represents a complete Merkle Search Tree (MST) for an AT Protocol repository.
/// 
/// The MST provides efficient key/value storage with the following properties:
/// - Content-addressed (deterministic CID for given content)
/// - Sorted key order (enables efficient range scans)
/// - Efficient updates (only changed nodes need to be rewritten)
/// - Cryptographically verifiable (Merkle tree properties)
/// 
/// This implementation supports:
/// - Adding, updating, and deleting records
/// - Looking up records by key
/// - Serializing to/from CAR format
/// - Computing diffs between versions
/// </summary>
public class MerkleSearchTree
{
    /// <summary>
    /// The root node of the MST.
    /// Can be null for an empty tree.
    /// </summary>
    public MstNode? Root { get; private set; }

    /// <summary>
    /// In-memory store of all nodes and records by CID.
    /// Used for fast lookups during tree operations.
    /// </summary>
    private Dictionary<string, MstNode> _nodeCache = new Dictionary<string, MstNode>();
    private Dictionary<string, byte[]> _recordCache = new Dictionary<string, byte[]>();

    /// <summary>
    /// Create an empty MST.
    /// </summary>
    public MerkleSearchTree()
    {
        Root = new MstNode(); // Empty node
    }

    /// <summary>
    /// Create an MST with an existing root node.
    /// </summary>
    public MerkleSearchTree(MstNode root)
    {
        Root = root;
    }

    /// <summary>
    /// Add or update a record in the MST.
    /// 
    /// The key is typically a repo path like "app.bsky.feed.post/3kj1..."
    /// The record is a DAG-CBOR encoded object.
    /// 
    /// Returns the new root CID after the update.
    /// </summary>
    public CidV1 Put(string key, byte[] record)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        
        // Hash the record to get its CID
        var recordCid = ComputeRecordCid(record);
        
        // Store record in cache
        _recordCache[recordCid.Base32] = record;

        // Insert into tree
        Root = PutEntry(Root ?? new MstNode(), keyBytes, recordCid, 0);
        
        // Recompute root CID and add to cache
        var rootCid = Root.ComputeCid();
        _nodeCache[rootCid.Base32] = Root;
        
        return rootCid;
    }

    /// <summary>
    /// Recursively insert an entry into the tree.
    /// </summary>
    private MstNode PutEntry(MstNode node, byte[] key, CidV1 valueCid, int depth)
    {
        int keyDepth = MstNode.GetKeyDepth(key);

        // If key belongs at this level
        if (keyDepth == depth)
        {
            // Find position to insert
            int insertPos = 0;
            byte[] prefix = Array.Empty<byte>();

            for (int i = 0; i < node.Entries.Count; i++)
            {
                var entry = node.Entries[i];
                byte[] entryFullKey = entry.GetFullKey(prefix);
                
                int comparison = CompareKeys(key, entryFullKey);
                
                if (comparison == 0)
                {
                    // Update existing entry
                    node.Entries[i].ValueCid = valueCid;
                    return node;
                }
                else if (comparison < 0)
                {
                    // Insert before this entry
                    insertPos = i;
                    break;
                }
                
                prefix = entryFullKey;
                insertPos = i + 1;
            }

            // Calculate prefix length for new entry
            int prefixLen = 0;
            if (insertPos > 0)
            {
                var prevEntry = node.Entries[insertPos - 1];
                byte[] prevKey = prevEntry.GetFullKey(prefix);
                prefixLen = GetCommonPrefixLength(prevKey, key);
            }

            // Create new entry
            var newEntry = new MstEntry
            {
                PrefixLength = prefixLen,
                KeySuffix = key.Skip(prefixLen).ToArray(),
                ValueCid = valueCid
            };

            node.Entries.Insert(insertPos, newEntry);

            // Update prefix lengths of following entries if needed
            if (insertPos < node.Entries.Count - 1)
            {
                UpdatePrefixLengths(node, insertPos);
            }

            return node;
        }
        else
        {
            // Key belongs at a deeper level - find the right subtree
            int pos = 0;
            byte[] prefix = Array.Empty<byte>();

            for (int i = 0; i < node.Entries.Count; i++)
            {
                var entry = node.Entries[i];
                byte[] entryKey = entry.GetFullKey(prefix);
                
                if (CompareKeys(key, entryKey) < 0)
                {
                    break;
                }
                
                prefix = entryKey;
                pos = i + 1;
            }

            // Insert into appropriate subtree
            if (pos == 0)
            {
                // Goes in left subtree
                var leftNode = node.LeftCid != null && _nodeCache.TryGetValue(node.LeftCid.Base32, out var cached)
                    ? cached
                    : new MstNode();
                
                var newLeft = PutEntry(leftNode, key, valueCid, depth + 1);
                node.LeftCid = newLeft.ComputeCid();
                _nodeCache[node.LeftCid.Base32] = newLeft;
            }
            else
            {
                // Goes in right subtree of entry[pos-1]
                var entry = node.Entries[pos - 1];
                var treeNode = entry.TreeCid != null && _nodeCache.TryGetValue(entry.TreeCid.Base32, out var cached)
                    ? cached
                    : new MstNode();
                
                var newTree = PutEntry(treeNode, key, valueCid, depth + 1);
                entry.TreeCid = newTree.ComputeCid();
                _nodeCache[entry.TreeCid.Base32] = newTree;
            }

            return node;
        }
    }

    /// <summary>
    /// Delete a record from the MST.
    /// Returns the new root CID, or null if tree is empty.
    /// </summary>
    public CidV1? Delete(string key)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        
        Root = DeleteEntry(Root, keyBytes, 0);
        
        if (Root == null || (Root.Entries.Count == 0 && Root.LeftCid == null))
        {
            Root = new MstNode(); // Empty tree
        }

        return Root?.ComputeCid();
    }

    /// <summary>
    /// Recursively delete an entry from the tree.
    /// </summary>
    private MstNode? DeleteEntry(MstNode? node, byte[] key, int depth)
    {
        if (node == null)
        {
            return null;
        }

        int keyDepth = MstNode.GetKeyDepth(key);

        if (keyDepth == depth)
        {
            // Find and remove the entry
            byte[] prefix = Array.Empty<byte>();
            
            for (int i = 0; i < node.Entries.Count; i++)
            {
                var entry = node.Entries[i];
                byte[] entryKey = entry.GetFullKey(prefix);
                
                if (CompareKeys(key, entryKey) == 0)
                {
                    node.Entries.RemoveAt(i);
                    
                    // Update prefix lengths
                    if (i < node.Entries.Count)
                    {
                        UpdatePrefixLengths(node, i);
                    }
                    
                    return node;
                }
                
                prefix = entryKey;
            }
        }
        else
        {
            // Search in subtrees
            int pos = 0;
            byte[] prefix = Array.Empty<byte>();

            for (int i = 0; i < node.Entries.Count; i++)
            {
                var entry = node.Entries[i];
                byte[] entryKey = entry.GetFullKey(prefix);
                
                if (CompareKeys(key, entryKey) < 0)
                {
                    break;
                }
                
                prefix = entryKey;
                pos = i + 1;
            }

            if (pos == 0)
            {
                // Search in left subtree
                if (node.LeftCid != null && _nodeCache.TryGetValue(node.LeftCid.Base32, out var leftNode))
                {
                    var newLeft = DeleteEntry(leftNode, key, depth + 1);
                    node.LeftCid = newLeft?.ComputeCid();
                }
            }
            else
            {
                // Search in right subtree
                var entry = node.Entries[pos - 1];
                if (entry.TreeCid != null && _nodeCache.TryGetValue(entry.TreeCid.Base32, out var treeNode))
                {
                    var newTree = DeleteEntry(treeNode, key, depth + 1);
                    entry.TreeCid = newTree?.ComputeCid();
                }
            }
        }

        return node;
    }

    /// <summary>
    /// Get a record by key.
    /// Returns null if not found.
    /// </summary>
    public byte[]? Get(string key)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        var cid = GetCid(Root, keyBytes, 0);
        
        if (cid != null && _recordCache.TryGetValue(cid.Base32, out var record))
        {
            return record;
        }
        
        return null;
    }

    /// <summary>
    /// Get the CID of a record by key.
    /// </summary>
    private CidV1? GetCid(MstNode? node, byte[] key, int depth)
    {
        if (node == null)
        {
            return null;
        }

        // Check left subtree first
        if (node.LeftCid != null && _nodeCache.TryGetValue(node.LeftCid.Base32, out var leftNode))
        {
            var leftResult = GetCid(leftNode, key, depth + 1);
            if (leftResult != null)
            {
                return leftResult;
            }
        }

        // Check entries at this level
        byte[] prevKey = Array.Empty<byte>();
        
        foreach (var entry in node.Entries)
        {
            // Reconstruct the entry's full key using PrefixLength
            byte[] entryPrefix = new byte[entry.PrefixLength];
            if (entry.PrefixLength > 0 && prevKey.Length >= entry.PrefixLength)
            {
                Array.Copy(prevKey, 0, entryPrefix, 0, entry.PrefixLength);
            }
            
            // Build full key from prefix + suffix
            byte[] entryKey = new byte[entryPrefix.Length + entry.KeySuffix.Length];
            Array.Copy(entryPrefix, 0, entryKey, 0, entryPrefix.Length);
            Array.Copy(entry.KeySuffix, 0, entryKey, entryPrefix.Length, entry.KeySuffix.Length);
            
            if (CompareKeys(key, entryKey) == 0)
            {
                // Found exact match!
                return entry.ValueCid;
            }
            
            prevKey = entryKey;

            // Check right subtree of this entry
            if (entry.TreeCid != null && _nodeCache.TryGetValue(entry.TreeCid.Base32, out var treeNode))
            {
                var treeResult = GetCid(treeNode, key, depth + 1);
                if (treeResult != null)
                {
                    return treeResult;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// List all keys in the tree (in sorted order).
    /// </summary>
    public List<string> ListKeys()
    {
        var keys = new List<string>();
        ListKeysRecursive(Root, Array.Empty<byte>(), keys);
        return keys;
    }

    private void ListKeysRecursive(MstNode? node, byte[] prefix, List<string> keys)
    {
        if (node == null)
        {
            return;
        }

        // Visit left subtree
        if (node.LeftCid != null && _nodeCache.TryGetValue(node.LeftCid.Base32, out var leftNode))
        {
            ListKeysRecursive(leftNode, prefix, keys);
        }

        // Visit entries - track the previous key for prefix compression
        byte[] prevKey = prefix;
        foreach (var entry in node.Entries)
        {
            // Use PrefixLength to get the right amount from previous key
            byte[] entryPrefix = new byte[entry.PrefixLength];
            if (entry.PrefixLength > 0 && prevKey.Length >= entry.PrefixLength)
            {
                Array.Copy(prevKey, 0, entryPrefix, 0, entry.PrefixLength);
            }
            
            // Build full key from prefix + suffix
            byte[] entryKey = new byte[entryPrefix.Length + entry.KeySuffix.Length];
            Array.Copy(entryPrefix, 0, entryKey, 0, entryPrefix.Length);
            Array.Copy(entry.KeySuffix, 0, entryKey, entryPrefix.Length, entry.KeySuffix.Length);
            
            keys.Add(Encoding.UTF8.GetString(entryKey));
            prevKey = entryKey;

            // Visit right subtree
            if (entry.TreeCid != null && _nodeCache.TryGetValue(entry.TreeCid.Base32, out var treeNode))
            {
                ListKeysRecursive(treeNode, entryKey, keys);
            }
        }
    }

    /// <summary>
    /// Compute CID for a record.
    /// </summary>
    private CidV1 ComputeRecordCid(byte[] record)
    {
        var hash = SHA256.HashData(record);

        var cid = new CidV1
        {
            Version = new VarInt { Value = 1 },
            Multicodec = new VarInt { Value = 0x71 }, // dag-cbor
            HashFunction = new VarInt { Value = 0x12 }, // sha256
            DigestSize = new VarInt { Value = 32 },
            DigestBytes = hash,
            AllBytes = Array.Empty<byte>(),
            Base32 = ""
        };

        using var ms = new MemoryStream();
        CidV1.WriteCid(ms, cid);
        cid.AllBytes = ms.ToArray();
        cid.Base32 = "b" + Base32Encoding.BytesToBase32(cid.AllBytes);

        return cid;
    }

    /// <summary>
    /// Compare two byte array keys lexicographically.
    /// </summary>
    private int CompareKeys(byte[] a, byte[] b)
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
    /// Get the length of the common prefix between two keys.
    /// </summary>
    private int GetCommonPrefixLength(byte[] a, byte[] b)
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
    /// Update prefix lengths for entries starting at the given position.
    /// </summary>
    private void UpdatePrefixLengths(MstNode node, int startPos)
    {
        if (startPos >= node.Entries.Count)
        {
            return;
        }

        byte[] prefix = Array.Empty<byte>();
        
        // Build up prefix from entries before startPos
        for (int i = 0; i < startPos; i++)
        {
            prefix = node.Entries[i].GetFullKey(prefix);
        }

        // Update entries from startPos onwards
        for (int i = startPos; i < node.Entries.Count; i++)
        {
            var entry = node.Entries[i];
            byte[] fullKey = entry.GetFullKey(prefix);
            
            // Recalculate prefix length
            if (i > 0)
            {
                int prefixLen = GetCommonPrefixLength(prefix, fullKey);
                entry.PrefixLength = prefixLen;
                entry.KeySuffix = fullKey.Skip(prefixLen).ToArray();
            }
            else
            {
                entry.PrefixLength = 0;
                entry.KeySuffix = fullKey;
            }
            
            prefix = fullKey;
        }
    }

    /// <summary>
    /// Load a node from the cache (used when deserializing from CAR).
    /// </summary>
    public void LoadNode(CidV1 cid, MstNode node)
    {
        _nodeCache[cid.Base32] = node;
        node.Cid = cid;
    }

    /// <summary>
    /// Load a record into the cache.
    /// </summary>
    public void LoadRecord(CidV1 cid, byte[] record)
    {
        _recordCache[cid.Base32] = record;
    }

    /// <summary>
    /// Set the root node (used when loading from CAR files).
    /// </summary>
    public void SetRoot(MstNode? root)
    {
        Root = root;
    }

    /// <summary>
    /// Get all nodes in the tree (for serialization to CAR).
    /// </summary>
    public Dictionary<string, MstNode> GetAllNodes()
    {
        return new Dictionary<string, MstNode>(_nodeCache);
    }

    /// <summary>
    /// Get all records in the tree (for serialization to CAR).
    /// </summary>
    public Dictionary<string, byte[]> GetAllRecords()
    {
        return new Dictionary<string, byte[]>(_recordCache);
    }

    /// <summary>
    /// Get a node by its CID.
    /// </summary>
    public MstNode? GetNodeByCid(string cidBase32)
    {
        return _nodeCache.TryGetValue(cidBase32, out var node) ? node : null;
    }

    /// <summary>
    /// Get changed blocks (nodes and records) between two MST roots.
    /// Returns dictionary of CID -> block bytes for only the changed data.
    /// </summary>
    public Dictionary<string, byte[]> GetChangedBlocks(CidV1? oldRootCid, CidV1? newRootCid, HashSet<string> changedRecordCids)
    {
        var changedBlocks = new Dictionary<string, byte[]>();

        if (newRootCid == null)
        {
            return changedBlocks;
        }

        MstNode? oldRoot = null;
        if (oldRootCid != null && _nodeCache.TryGetValue(oldRootCid.Base32, out var oldRootNode))
        {
            oldRoot = oldRootNode;
        }

        MstNode? newRoot = _nodeCache.TryGetValue(newRootCid.Base32, out var newRootNode) ? newRootNode : Root;

        if (newRoot == null)
        {
            return changedBlocks;
        }

        // Recursively find changed nodes
        CollectChangedNodes(oldRoot, newRoot, changedBlocks);

        // Add changed records
        foreach (var recordCid in changedRecordCids)
        {
            if (_recordCache.TryGetValue(recordCid, out var recordData))
            {
                changedBlocks[recordCid] = recordData;
            }
        }

        return changedBlocks;
    }

    /// <summary>
    /// Recursively collect nodes that have changed between old and new trees.
    /// </summary>
    private void CollectChangedNodes(MstNode? oldNode, MstNode? newNode, Dictionary<string, byte[]> changedBlocks)
    {
        if (newNode == null || newNode.Cid == null)
        {
            return;
        }

        // If old node is null or CIDs differ, this node has changed
        if (oldNode == null || oldNode.Cid == null || oldNode.Cid.Base32 != newNode.Cid.Base32)
        {
            // Add this node to changed blocks
            changedBlocks[newNode.Cid.Base32] = newNode.ToDagCbor();

            // If old node exists with different CID, we need to traverse both trees
            // to find all changed subtrees
            if (oldNode != null)
            {
                // Compare left subtrees
                MstNode? oldLeft = null;
                if (oldNode.LeftCid != null && _nodeCache.TryGetValue(oldNode.LeftCid.Base32, out var oldLeftNode))
                {
                    oldLeft = oldLeftNode;
                }

                MstNode? newLeft = null;
                if (newNode.LeftCid != null && _nodeCache.TryGetValue(newNode.LeftCid.Base32, out var newLeftNode))
                {
                    newLeft = newLeftNode;
                }

                if (newLeft != null)
                {
                    CollectChangedNodes(oldLeft, newLeft, changedBlocks);
                }

                // Compare entry subtrees
                var oldEntryMap = new Dictionary<string, MstEntry>();
                if (oldNode.Entries != null)
                {
                    byte[] prefix = Array.Empty<byte>();
                    foreach (var entry in oldNode.Entries)
                    {
                        byte[] fullKey = entry.GetFullKey(prefix);
                        oldEntryMap[Encoding.UTF8.GetString(fullKey)] = entry;
                        prefix = fullKey;
                    }
                }

                if (newNode.Entries != null)
                {
                    byte[] prefix = Array.Empty<byte>();
                    foreach (var newEntry in newNode.Entries)
                    {
                        byte[] fullKey = newEntry.GetFullKey(prefix);
                        string key = Encoding.UTF8.GetString(fullKey);

                        MstEntry? oldEntry = null;
                        oldEntryMap.TryGetValue(key, out oldEntry);

                        // Check if this entry has a tree child that needs traversal
                        if (newEntry.TreeCid != null && _nodeCache.TryGetValue(newEntry.TreeCid.Base32, out var newTreeNode))
                        {
                            MstNode? oldTreeNode = null;
                            if (oldEntry?.TreeCid != null && _nodeCache.TryGetValue(oldEntry.TreeCid.Base32, out var oldTree))
                            {
                                oldTreeNode = oldTree;
                            }

                            CollectChangedNodes(oldTreeNode, newTreeNode, changedBlocks);
                        }

                        prefix = fullKey;
                    }
                }
            }
            else
            {
                // No old node - this is a new subtree, collect all nodes
                CollectAllNodesInSubtree(newNode, changedBlocks);
            }
        }
        // If CIDs match, subtree is unchanged - don't traverse
    }

    /// <summary>
    /// Collect all nodes in a subtree (used when entire subtree is new).
    /// </summary>
    private void CollectAllNodesInSubtree(MstNode node, Dictionary<string, byte[]> blocks)
    {
        if (node == null || node.Cid == null)
        {
            return;
        }

        // Add this node if not already added
        if (!blocks.ContainsKey(node.Cid.Base32))
        {
            blocks[node.Cid.Base32] = node.ToDagCbor();

            // Traverse left subtree
            if (node.LeftCid != null && _nodeCache.TryGetValue(node.LeftCid.Base32, out var leftNode))
            {
                CollectAllNodesInSubtree(leftNode, blocks);
            }

            // Traverse entry subtrees
            if (node.Entries != null)
            {
                foreach (var entry in node.Entries)
                {
                    if (entry.TreeCid != null && _nodeCache.TryGetValue(entry.TreeCid.Base32, out var treeNode))
                    {
                        CollectAllNodesInSubtree(treeNode, blocks);
                    }
                }
            }
        }
    }
}
