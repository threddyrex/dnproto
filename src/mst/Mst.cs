

using System.Security.Cryptography;
using System.Text;


namespace dnproto.mst;

/// <summary>
/// In-memory representation of a Merkle Search Tree (MST).
/// 
/// The main purpose of this class is to codify the properties of
/// a MST (key depth, key sorting, etc.), and to assemble a
/// MST with those properties from a flat list of items you provide.
/// 
/// You start with a list of MstItems. These are key/value pairs that
/// you can store offline in a db or elsewhere. 
/// 
/// Once you want to run MST operations, you can call Mst.AssembleTreeFromItems() 
/// to build the MST in memory, and work on that.
/// 
/// There are no "put" or "delete" operations here. Do those operations on your list 
/// of MstItems, then re-assemble the tree when you need to work with it.
/// 
/// Atproto uses MST for the repos (https://atproto.com/specs/repository)
/// 
/// </summary>
public class Mst
{
    public required MstNode Root;


    #region ASSEMBLE

    /// <summary>
    /// Assemble a Merkle Search Tree (MST) from a flat list of items.
    /// Once you get have the MST, you can run find operations on it.
    /// </summary>
    /// <param name="items"></param>
    /// <returns></returns>
    public static Mst AssembleTreeFromItems(List<MstItem> items)
    {
        if(items.Count == 0)
        {
            return new Mst() { Root = new MstNode() { KeyDepth = 0, Entries = new List<MstEntry>() } };
        }

        //
        // Get lists of items by key depth
        //
        Dictionary<int, List<MstItem>> itemsByDepth = new Dictionary<int, List<MstItem>>();
        foreach(var item in items)
        {
            int keyDepth = GetKeyDepth(item.Key);

            if(itemsByDepth.ContainsKey(keyDepth) == false)
            {
                itemsByDepth[keyDepth] = new List<MstItem>();
            }

            itemsByDepth[keyDepth].Add(item);
        }

        //
        // Get max key depth
        //
        int rootKeyDepth = itemsByDepth.Keys.Max();

        //
        // Create root for that depth.
        //
        // The largest key depth is the *root* of the tree.
        //
        var rootNode = new MstNode() { KeyDepth = rootKeyDepth, Entries = new List<MstEntry>() };


        //
        // insert items, in keyDepth order
        //
        for(int currentKeyDepth = rootKeyDepth; currentKeyDepth >= 0; currentKeyDepth--)
        {
            if(itemsByDepth.ContainsKey(currentKeyDepth))
            {
                foreach(var item in itemsByDepth[currentKeyDepth])
                {
                    AssembleItem(rootNode, item.Key, item.Value, GetKeyDepth(item.Key));
                }            
            }
        }

        //
        // Return
        //
        return new Mst() { Root = rootNode };
    }


    private static void AssembleItem(MstNode currentNode, string keyToAdd, string valueToAdd, int keyDepthToAdd)
    {

        //
        // Add at this node?
        //
        if(currentNode.KeyDepth == keyDepthToAdd)
        {
            //
            // Get insert index
            //
            int insertIndex = 0;
            foreach(var entry in currentNode.Entries)
            {
                if(LessThan(keyToAdd, entry.Key))
                {
                    break;
                }

                insertIndex++;
            }

            currentNode.Entries.Insert(insertIndex, new MstEntry() { Key = keyToAdd, Value = valueToAdd });
        }
        else
        {
            //
            // Get insert index
            //
            int insertIndex = 0;
            foreach(var entry in currentNode.Entries)
            {
                if(LessThan(keyToAdd, entry.Key))
                {
                    break;
                }

                insertIndex++;
            }

            //
            // Go left?
            //
            if (insertIndex == 0)
            {
                if (currentNode.LeftTree == null)
                {
                    currentNode.LeftTree = new MstNode() { KeyDepth = currentNode.KeyDepth - 1, Entries = new List<MstEntry>() };
                }

                AssembleItem(currentNode.LeftTree, keyToAdd, valueToAdd, keyDepthToAdd);
            }
            //
            // Go right?
            //
            else
            {
                if(currentNode.Entries[insertIndex - 1].RightTree == null)
                {
                    currentNode.Entries[insertIndex - 1].RightTree = new MstNode() { KeyDepth = currentNode.KeyDepth - 1, Entries = new List<MstEntry>() };
                }

                AssembleItem(currentNode.Entries[insertIndex - 1].RightTree!, keyToAdd, valueToAdd, keyDepthToAdd);
            }
        }
    }


    #endregion


    #region FIND

    /// <summary>
    /// Find all nodes that would be traversed to find the given key.
    /// This helps us identify which nodes to include in firehose events.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public List<MstNode> FindNodesForKey(string key)
    {
        List<MstNode> foundNodes = new List<MstNode>();
        InternalFindNodesForKey(key, Mst.GetKeyDepth(key), Root, foundNodes);
        return foundNodes;
    }

    private void InternalFindNodesForKey(string targetKey, int targetKeyDepth, MstNode currentNode, List<MstNode> foundNodes)
    {
        //
        // Add this one
        //
        foundNodes.Add(currentNode);


        //
        // If we're here, we're done
        //
        if(currentNode.KeyDepth == targetKeyDepth)
        {
            return;
        }
        else
        {
            //
            // Find index
            //
            int entryIndex = 0;
            foreach(var entry in currentNode.Entries)
            {
                if(LessThan(targetKey, entry.Key))
                {
                    break;
                }

                entryIndex++;
            }


            //
            // Go left?
            //
            if (entryIndex == 0)
            {
                if (currentNode.LeftTree != null)
                {
                    InternalFindNodesForKey(targetKey, targetKeyDepth, currentNode.LeftTree, foundNodes);
                }
            }
            //
            // Go right?
            //
            else
            {
                if (currentNode.Entries[entryIndex - 1].RightTree != null)
                {
                    InternalFindNodesForKey(targetKey, targetKeyDepth, currentNode.Entries[entryIndex - 1].RightTree!, foundNodes);
                }
            }
        }
    }

    public List<MstNode> FindAllNodes()
    {
        List<MstNode> foundNodes = new List<MstNode>();
        InternalFindAllNodes(Root, foundNodes);
        return foundNodes;
    }

    private void InternalFindAllNodes(MstNode currentNode, List<MstNode> foundNodes)
    {
        foundNodes.Add(currentNode);

        if(currentNode.LeftTree != null)
        {
            InternalFindAllNodes(currentNode.LeftTree, foundNodes);
        }

        foreach(var entry in currentNode.Entries)
        {
            if(entry.RightTree != null)
            {
                InternalFindAllNodes(entry.RightTree, foundNodes);
            }
        }
    }


    #endregion




    #region KEYCOMPARE

    /// <summary>
    /// Compare two keys. Used when assembling a tree.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
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

    
    public static bool LessThan(string a, string b)
    {
        return CompareKeys(a, b) < 0;
    }

    public static bool GreaterThan(string a, string b)
    {
        return CompareKeys(a, b) > 0;
    }

    public static bool KeysEqual(string a, string b)
    {
        return CompareKeys(a, b) == 0;
    }
    #endregion

    #region KEYDEPTH

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

    #endregion

}