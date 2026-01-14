

using System.Security.Cryptography;
using System.Text;


namespace dnproto.mst;

/// <summary>
/// In-memory representation of a Merkle Search Tree (MST).
/// </summary>
public class Mst
{
    public required MstNode Root;


    #region ASSEMBLE

    /// <summary>
    /// Assemble a Merkle Search Tree (MST) from a flat list of items.
    /// Caller can store these in a database.
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
        // Get max key depth
        //
        int rootKeyDepth = items.Max(i => GetKeyDepth(i.Key));

        //
        // Create root for that depth
        //
        var rootNode = new MstNode() { KeyDepth = rootKeyDepth, Entries = new List<MstEntry>() };


        //
        // insert items
        //
        foreach(var item in items)
        {
            AssembleItem(rootNode, item.Key, item.Value, GetKeyDepth(item.Key));
        }

        //
        // Return
        //
        return new Mst() { Root = rootNode };
    }


    private static void AssembleItem(MstNode currentNode, string keyToAdd, string valueToAdd, int keyDepthToAdd)
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
        // Add at this node?
        //
        if(currentNode.KeyDepth == keyDepthToAdd)
        {
            currentNode.Entries.Insert(insertIndex, new MstEntry() { Key = keyToAdd, Value = valueToAdd });
        }
        else
        {
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
                if(KeysEqual(targetKey, entry.Key))
                {
                    // found it.
                    return;
                }

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
    public static int CompareKeys(string a, string b)
    {
        return string.CompareOrdinal(a, b);
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