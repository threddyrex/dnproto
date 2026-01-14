
using dnproto.fs;
using dnproto.log;
using dnproto.repo;
using Microsoft.Extensions.Logging;


namespace dnproto.pds;


/// <summary>
/// MST implementation, backed by database.
/// 
/// Instance methods usually modify the MST stored in the database (_db).
/// 
/// </summary>
public class MstDb
{
    private LocalFileSystem _lfs;
    private IDnProtoLogger _logger;
    private PdsDb _db;


    private MstDb(LocalFileSystem lfs, IDnProtoLogger logger, PdsDb db)
    {
        _lfs = lfs;
        _db = db;
        _logger = logger;
    }

    public static MstDb ConnectMstDb(LocalFileSystem lfs, IDnProtoLogger logger, PdsDb db)
    {
        return new MstDb(lfs, logger, db);
    }


    #region GET

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


    #endregion



    #region PUT

    /// <summary>
    /// Put an entry into the MST stored in the database.
    /// It recursively calls InternalPutEntry to find the right place to insert the entry.
    /// For the part of the tree where an update happens, all of the cids from that location 
    /// all the way to the root *will be* recalculated. 
    /// (because a cid is a hash of the node and its entries).
    /// Db updates happen inside InternalPutEntry.
    /// 
    /// MST Layer Logic (per AT Protocol spec):
    /// - Each key has a "depth" = number of leading zero pairs in SHA-256 hash
    /// - Higher depth keys are stored at higher layers (closer to root)
    /// - Root node layer = max depth of any key in the tree
    /// - Children have layer = parent layer - 1
    /// </summary>
    /// <param name="db"></param>
    /// <param name="key"></param>
    /// <param name="recordCid"></param>
    public (CidV1 originalRootMstNodeCid, CidV1 newRootMstNodeCid, List<Guid> updatedNodeObjectIds) 
        PutEntry(string key, CidV1 recordCid)
    {
        //
        // Load from db
        //
        var repoCommit = _db.GetRepoCommit();
        var mstNodeRoot = _db.GetMstNodeByCid(repoCommit!.RootMstNodeCid!);
        var mstNodeRootEntries = _db.GetMstEntriesForNodeObjectId((Guid)mstNodeRoot!.NodeObjectId!);
        var originalRootMstNodeCid = repoCommit!.RootMstNodeCid!;

        //
        // Calculate the key depth and current root layer
        //
        int keyDepth = MstEntry.GetKeyDepth(key);
        int rootLayer = GetRootLayer(mstNodeRoot, mstNodeRootEntries);

        //
        // If the key depth is higher than the current root layer,
        // we need to create new parent nodes to accommodate it.
        //
        List<Guid> updatedNodeObjectIds = new List<Guid>();
        while (keyDepth > rootLayer)
        {
            // Create a new parent node with the current root as its left child
            var newParent = new MstNode();
            newParent.NodeObjectId = Guid.NewGuid();
            newParent.LeftMstNodeCid = mstNodeRoot!.Cid;
            newParent.RecomputeCid(new List<MstEntry>());
            _db.InsertMstNode(newParent);
            updatedNodeObjectIds.Add((Guid)newParent.NodeObjectId!);

            // Update root reference
            mstNodeRoot = newParent;
            mstNodeRootEntries = new List<MstEntry>();
            rootLayer++;
        }

        //
        // Recursive put - now using layer (higher = closer to root)
        //
        InternalPutEntry(key, recordCid, mstNodeRoot!, mstNodeRootEntries, currentLayer: rootLayer, updatedNodeObjectIds);

        var newRootMstNodeCid = mstNodeRoot!.Cid!;

        //
        // Update repo commit if root changed
        //
        if (!originalRootMstNodeCid.Equals(newRootMstNodeCid))
        {
            repoCommit.RootMstNodeCid = newRootMstNodeCid;
            _db.UpdateRepoCommit(repoCommit);
        }

        //
        // Return
        //
        return (originalRootMstNodeCid, newRootMstNodeCid, updatedNodeObjectIds);
    }

    /// <summary>
    /// Calculate the layer of the root node based on the highest key depth in the tree.
    /// If the tree is empty, returns 0.
    /// </summary>
    private int GetRootLayer(MstNode rootNode, List<MstEntry> rootEntries)
    {
        // If root has entries, the layer is the depth of the first key
        if (rootEntries.Count > 0)
        {
            string firstKey = rootEntries[0].KeySuffix ?? string.Empty;
            return MstEntry.GetKeyDepth(firstKey);
        }

        // If root has a left child, recurse to find layer
        if (rootNode.LeftMstNodeCid != null)
        {
            var leftNode = _db.GetMstNodeByCid(rootNode.LeftMstNodeCid);
            if (leftNode != null)
            {
                var leftEntries = _db.GetMstEntriesForNodeObjectId((Guid)leftNode.NodeObjectId!);
                return GetRootLayer(leftNode, leftEntries) + 1;
            }
        }

        // Empty tree
        return 0;
    }



    /// <summary>
    /// Internal recursive function to put an entry into the MST.
    /// Caller passes in currentNode and currentEntries. 
    /// On return, currentNode and currentEntries are updated in db.
    /// 
    /// Possible scenarios for one iteration:
    /// 
    ///     1) this level - update existing MstEntry with identical key (update)
    ///     2) this level - insert new MstEntry (insert)
    ///     3) next level - insert into MstNode.LeftMstNodeCid (go left)
    ///     4) next level - insert into MstEntry.TreeMstNodeCid (go right)
    /// 
    /// Note: currentLayer represents the MST layer where higher values are closer to root.
    /// Keys with depth == currentLayer belong at this node.
    /// Keys with depth < currentLayer go to child nodes (layer - 1).
    /// </summary>
    private void InternalPutEntry(string recordKeyToInsert, CidV1 recordCidToInsert, MstNode currentNode, List<MstEntry> currentEntries, int currentLayer, List<Guid> updatedNodeObjectIds)
    {
        //
        // Calculate full keys and depth.
        //
        var currentEntryKeys = MstEntry.GetFullKeys(currentEntries);
        int keyDepthToInsert = MstEntry.GetKeyDepth(recordKeyToInsert);

        //
        // Insert at this level?
        // Key belongs here if its depth equals the current layer.
        //
        if(keyDepthToInsert == currentLayer)
        {
            int insertIndex = 0;

            //
            // Loop through entries to find insert position.
            //
            for(int i = 0; i < currentEntries.Count; i++)
            {
                var entry = currentEntries[i];
                string fullKey = currentEntryKeys[i];
                int comparison = MstEntry.CompareKeys(recordKeyToInsert, fullKey);

                //
                // CASE 1 - update existing MstEntry with identical key (update)
                //
                if(comparison == 0)
                {
                    entry.RecordCid = recordCidToInsert;
                    currentNode.RecomputeCid(currentEntries);
                    _db.ReplaceMstNode(currentNode, currentEntries);
                    updatedNodeObjectIds.Add((Guid) currentNode.NodeObjectId!);
                    return;
                }
                else if(comparison < 0)
                {
                    // Insert new entry before this one
                    insertIndex = i;
                    break;
                }

                insertIndex++;
            }

            //
            // CASE 2 - insert new MstEntry (insert)
            //
            var newEntry = new MstEntry
            {
                RecordCid = recordCidToInsert,
                TreeMstNodeCid = null,
                EntryIndex = insertIndex, // gets fixed later
                KeySuffix = recordKeyToInsert, // gets fixed later
                PrefixLength = 0, // gets fixed later, but start at 0 to make "GetFullKeys" work
            };

            // insert first, before fixing indexes and prefix lengths
            currentEntries.Insert(insertIndex, newEntry);
            currentEntryKeys.Insert(insertIndex, recordKeyToInsert);

            // fix up
            MstEntry.FixEntryIndexes(currentEntries);
            MstEntry.FixPrefixLengths(currentEntries);

            // log debug
            for(int i = 0; i < currentEntries.Count; i++)
            {
                var entry = currentEntries[i];
                _logger.LogTrace($"   !! MST Entry {i}: EntryIndex={entry.EntryIndex}, PrefixLength={entry.PrefixLength}, KeySuffix={entry.KeySuffix}");
            }

            currentNode.RecomputeCid(currentEntries);
            _db.ReplaceMstNode(currentNode, currentEntries);
            updatedNodeObjectIds.Add((Guid) currentNode.NodeObjectId!);
            return;

        }
        //
        // Else: Need to go to next level
        //
        else
        {
            //
            // Find insert position
            //
            int insertPos = 0;
            for (int i = 0; i < currentEntries.Count; i++)
            {
                var entry = currentEntries[i];
                string entryKey = currentEntryKeys[i];
                
                if (MstEntry.CompareKeys(recordKeyToInsert, entryKey) < 0)
                {
                    break;
                }
                
                insertPos = i + 1;
            }


            //
            // CASE 3 - insert into MstNode.LeftMstNodeCid (go left)
            //
            if(insertPos == 0)
            {
                //
                // Get (or create) left subtree
                //
                MstNode? leftNode = null;                
                if (currentNode.LeftMstNodeCid != null)
                {
                    leftNode = _db.GetMstNodeByCid(currentNode.LeftMstNodeCid);
                }
                else
                {
                    leftNode = new MstNode();
                    leftNode.NodeObjectId = Guid.NewGuid();
                    leftNode.RecomputeCid(new List<MstEntry>());
                    _db.InsertMstNode(leftNode);
                    currentNode.LeftMstNodeCid = leftNode.Cid;
                }

                if(leftNode is null) 
                {
                    throw new Exception("Left MST node is null after creation.");
                }

                List<MstEntry> leftEntries = _db.GetMstEntriesForNodeObjectId((Guid)leftNode.NodeObjectId!);


                //
                // Recurse - child layer is currentLayer - 1 (lower layers are deeper in tree)
                //
                InternalPutEntry(recordKeyToInsert, recordCidToInsert, leftNode!, leftEntries, currentLayer - 1, updatedNodeObjectIds);


                //
                // Update currentNode (because its LeftMstNodeCid changed)
                //
                currentNode.LeftMstNodeCid = leftNode.Cid;
                currentNode.RecomputeCid(currentEntries);
                _db.ReplaceMstNode(currentNode, currentEntries);
                updatedNodeObjectIds.Add((Guid) currentNode.NodeObjectId!);
                return;

            }
            //
            // CASE 4 - insert into MstEntry.TreeMstNodeCid (go right)
            //
            else
            {
                // Go to right subtree of the selected entry
                MstNode? rightNode = null;
                if (currentEntries[insertPos - 1].TreeMstNodeCid != null)
                {
                    rightNode = _db.GetMstNodeByCid(currentEntries[insertPos - 1].TreeMstNodeCid);
                }
                else
                {
                    //
                    // Create new right node
                    //
                    rightNode = new MstNode();
                    rightNode.NodeObjectId = Guid.NewGuid();
                    rightNode.RecomputeCid(new List<MstEntry>());
                    _db.InsertMstNode(rightNode);
                    currentEntries[insertPos - 1].TreeMstNodeCid = rightNode.Cid;
                }

                List<MstEntry> rightEntries = _db.GetMstEntriesForNodeObjectId((Guid)rightNode?.NodeObjectId!);

                //
                // Recurse - child layer is currentLayer - 1 (lower layers are deeper in tree)
                //
                InternalPutEntry(recordKeyToInsert, recordCidToInsert, rightNode!, rightEntries, currentLayer - 1, updatedNodeObjectIds);

                //
                // Update currentNode (because currentEntries[insertPos - 1].TreeMstNodeCid changed)
                //
                currentEntries[insertPos - 1].TreeMstNodeCid = rightNode.Cid;
                currentNode.RecomputeCid(currentEntries);
                _db.ReplaceMstNode(currentNode, currentEntries);
                updatedNodeObjectIds.Add((Guid) currentNode.NodeObjectId!);
                return;
            }
        }
    }
    #endregion




    #region DELETE

    public (CidV1 originalRootMstNodeCid, CidV1 newRootMstNodeCid, List<Guid> updatedNodeObjectIds) 
        DeleteEntry(string recordKeyToDelete)
    {
        _logger.LogTrace($"MstDb.DeleteEntry: Deleting key: {recordKeyToDelete}");

        //
        // Load from db
        //
        var repoCommit = _db.GetRepoCommit();
        var mstNodeRoot = _db.GetMstNodeByCid(repoCommit!.RootMstNodeCid!);
        var mstNodeRootEntries = _db.GetMstEntriesForNodeObjectId((Guid)mstNodeRoot!.NodeObjectId!);
        var originalRootMstNodeCid = repoCommit!.RootMstNodeCid!;

        //
        // Calculate root layer
        //
        int rootLayer = GetRootLayer(mstNodeRoot, mstNodeRootEntries);

        //
        // Recursive delete
        //
        List<Guid> updatedNodeObjectIds = new List<Guid>();
        InternalDeleteEntry(recordKeyToDelete, mstNodeRoot!, mstNodeRootEntries, currentLayer: rootLayer, updatedNodeObjectIds);

        var newRootMstNodeCid = mstNodeRoot!.Cid!;

        //
        // Return
        //
        return (originalRootMstNodeCid, newRootMstNodeCid, updatedNodeObjectIds);
    }



    /// <summary>
    /// Internal recursive function to delete an entry from the MST.
    /// Caller passes in currentNode and currentEntries.
    /// On return, currentNode and currentEntries are updated in db.
    /// 
    /// Possible scenarios for one iteration:
    /// 
    ///     1) this level - delete existing MstEntry with identical key (delete)
    ///     2) next level - delete from MstNode.LeftMstNodeCid (go left)
    ///     3) next level - delete from MstEntry.TreeMstNodeCid (go right)
    /// 
    /// Note: currentLayer represents the MST layer where higher values are closer to root.
    /// Keys with depth == currentLayer belong at this node.
    /// Keys with depth < currentLayer go to child nodes (layer - 1).
    /// </summary>
    /// <param name="recordKeyToDelete"></param>
    /// <param name="currentNode"></param>
    /// <param name="currentEntries"></param>
    /// <param name="currentLayer"></param>
    /// <param name="updatedNodeObjectIds"></param>
    private void InternalDeleteEntry(string recordKeyToDelete, MstNode? currentNode, List<MstEntry> currentEntries, int currentLayer, List<Guid> updatedNodeObjectIds)
    {
        if(currentNode is null)
        {
            return;
        }

        //
        // Calculate full keys and depth.
        //
        var currentEntryKeys = MstEntry.GetFullKeys(currentEntries);
        int keyDepthToDelete = MstEntry.GetKeyDepth(recordKeyToDelete);

        _logger.LogTrace($"MstDb.InternalDeleteEntry: currentLayer={currentLayer}, keyDepthToDelete={keyDepthToDelete}, currentNodeCid={currentNode.Cid?.Base32}");

        //
        // Delete from this level?
        // Key belongs here if its depth equals the current layer.
        //
        if(keyDepthToDelete == currentLayer)
        {
            //
            // Find entry to delete
            //
            int deleteIndex = -1;
            for(int i = 0; i < currentEntries.Count; i++)
            {
                var entry = currentEntries[i];
                string fullKey = currentEntryKeys[i];
                _logger.LogTrace($"     MstDb.InternalDeleteEntry: Checking entry {i}, fullKey={fullKey}, recordKeyToDelete={recordKeyToDelete}");
                int comparison = MstEntry.CompareKeys(recordKeyToDelete, fullKey);

                if(comparison == 0)
                {
                    deleteIndex = i;
                    break;
                }
            }

            _logger.LogTrace($"MstDb.InternalDeleteEntry: deleteIndex={deleteIndex}");

            //
            // If found, delete it
            //
            if(deleteIndex != -1)
            {
                //
                // Get the subtree from the entry being deleted (if any)
                //
                CidV1? deletedEntrySubtreeCid = currentEntries[deleteIndex].TreeMstNodeCid;

                //
                // Determine the left subtree to merge with:
                // - If deleteIndex == 0, it's currentNode.LeftMstNodeCid
                // - Otherwise, it's currentEntries[deleteIndex - 1].TreeMstNodeCid
                //
                CidV1? leftSubtreeCid;
                if (deleteIndex == 0)
                {
                    leftSubtreeCid = currentNode.LeftMstNodeCid;
                }
                else
                {
                    leftSubtreeCid = currentEntries[deleteIndex - 1].TreeMstNodeCid;
                }

                //
                // Merge the two subtrees
                //
                CidV1? mergedSubtreeCid = MergeMstNodes(leftSubtreeCid, deletedEntrySubtreeCid, updatedNodeObjectIds);

                //
                // Update the subtree pointer to the merged result
                //
                if (deleteIndex == 0)
                {
                    currentNode.LeftMstNodeCid = mergedSubtreeCid;
                }
                else
                {
                    currentEntries[deleteIndex - 1].TreeMstNodeCid = mergedSubtreeCid;
                }

                //
                // Now remove the entry
                //
                currentEntries.RemoveAt(deleteIndex);
                currentEntryKeys.RemoveAt(deleteIndex);

                // fix up
                MstEntry.FixEntryIndexes(currentEntries);
                MstEntry.FixPrefixLengths(currentEntries);

                currentNode.RecomputeCid(currentEntries);
                _db.ReplaceMstNode(currentNode, currentEntries);
                updatedNodeObjectIds.Add((Guid) currentNode.NodeObjectId!);
            }

            return;
        }
        //
        // Else: Need to go to next level
        //
        else
        {
            //
            // Find delete position to traverse.
            //
            int deletePos = 0;
            for (int i = 0; i < currentEntries.Count; i++)
            {
                var entry = currentEntries[i];
                string entryKey = currentEntryKeys[i];
                
                if (MstEntry.CompareKeys(recordKeyToDelete, entryKey) < 0)
                {
                    break;
                }
                
                deletePos = i + 1;
            }

            _logger.LogTrace($"MstDb.InternalDeleteEntry: next level, deletePos={deletePos}");

            //
            // If 0, go left.
            //
            if(deletePos == 0)
            {
                //
                // Get left subtree
                //
                if (currentNode.LeftMstNodeCid != null)
                {
                    var leftNode = _db.GetMstNodeByCid(currentNode.LeftMstNodeCid);
                    if(leftNode is null) { return; }
                    var leftEntries = _db.GetMstEntriesForNodeObjectId((Guid)leftNode!.NodeObjectId!);

                    //
                    // Recurse - child layer is currentLayer - 1 (lower layers are deeper in tree)
                    //
                    InternalDeleteEntry(recordKeyToDelete, leftNode, leftEntries, currentLayer - 1, updatedNodeObjectIds);

                    //
                    // Update currentNode (because its LeftMstNodeCid might have changed)
                    //
                    currentNode.LeftMstNodeCid = leftNode.Cid;
                    currentNode.RecomputeCid(currentEntries);
                    _db.ReplaceMstNode(currentNode, currentEntries);
                    updatedNodeObjectIds.Add((Guid) currentNode.NodeObjectId!);
                }
                else
                {
                    _logger.LogTrace($"MstDb.InternalDeleteEntry: LeftMstNodeCid is null, cannot go left.");
                }

                return;
            }
            //
            // Else, go right into the selected entry.
            //
            else
            {
                MstNode? rightNode = null;
                if (currentEntries[deletePos - 1].TreeMstNodeCid != null)
                {
                    rightNode = _db.GetMstNodeByCid(currentEntries[deletePos - 1].TreeMstNodeCid);
                }
                if(rightNode is null) 
                { 
                    _logger.LogTrace($"MstDb.InternalDeleteEntry: Right subtree node is null, cannot go right.");
                    return; 
                }

                var rightEntries = _db.GetMstEntriesForNodeObjectId((Guid)rightNode!.NodeObjectId!);

                //
                // Recurse - child layer is currentLayer - 1 (lower layers are deeper in tree)
                //
                InternalDeleteEntry(recordKeyToDelete, rightNode, rightEntries, currentLayer - 1, updatedNodeObjectIds);

                //
                // Update currentNode (because currentEntries[deletePos - 1].TreeMstNodeCid might have changed)
                //
                currentEntries[deletePos - 1].TreeMstNodeCid = rightNode.Cid;
                currentNode.RecomputeCid(currentEntries);
                _db.ReplaceMstNode(currentNode, currentEntries);
                updatedNodeObjectIds.Add((Guid) currentNode.NodeObjectId!);
                return;
            }
        }

    }


    /// <summary>
    /// Merge two MST subtrees into a single subtree.
    /// This is needed when deleting an entry that has a subtree pointer (TreeMstNodeCid).
    /// The left subtree from before the deleted entry must be merged with the 
    /// deleted entry's subtree.
    /// 
    /// The merge algorithm:
    /// 1. If either subtree is null, return the other
    /// 2. Otherwise, create a new node containing:
    ///    - All keys/vals from left node, followed by all keys/vals from right node
    ///    - Subtrees: all but the last from left, merged(last of left, first of right), all but first from right
    /// </summary>
    /// <param name="leftCid">CID of the left subtree (can be null)</param>
    /// <param name="rightCid">CID of the right subtree (can be null)</param>
    /// <param name="updatedNodeObjectIds">List to track newly created nodes</param>
    /// <returns>CID of the merged subtree, or null if both inputs are null</returns>
    private CidV1? MergeMstNodes(CidV1? leftCid, CidV1? rightCid, List<Guid> updatedNodeObjectIds)
    {
        // If either is null, return the other
        if (leftCid == null)
        {
            return rightCid;
        }
        if (rightCid == null)
        {
            return leftCid;
        }

        // Get both nodes
        var leftNode = _db.GetMstNodeByCid(leftCid);
        var rightNode = _db.GetMstNodeByCid(rightCid);

        if (leftNode == null || rightNode == null)
        {
            _logger.LogError($"MstDb.MergeMstNodes: Could not find node for CID. leftCid={leftCid?.Base32}, rightCid={rightCid?.Base32}");
            return leftCid ?? rightCid;
        }

        var leftEntries = _db.GetMstEntriesForNodeObjectId((Guid)leftNode.NodeObjectId!);
        var rightEntries = _db.GetMstEntriesForNodeObjectId((Guid)rightNode.NodeObjectId!);

        _logger.LogTrace($"MstDb.MergeMstNodes: Merging left node ({leftEntries.Count} entries) with right node ({rightEntries.Count} entries)");

        // Recursively merge the rightmost subtree of left with the leftmost subtree of right
        CidV1? leftRightmostSubtree = leftEntries.Count > 0 
            ? leftEntries[leftEntries.Count - 1].TreeMstNodeCid 
            : leftNode.LeftMstNodeCid;
        CidV1? rightLeftmostSubtree = rightNode.LeftMstNodeCid;

        CidV1? mergedMiddleSubtree = MergeMstNodes(leftRightmostSubtree, rightLeftmostSubtree, updatedNodeObjectIds);

        // Create the merged node
        var mergedNode = new MstNode();
        mergedNode.NodeObjectId = Guid.NewGuid();
        mergedNode.LeftMstNodeCid = leftNode.LeftMstNodeCid;

        // Build merged entries list
        var mergedEntries = new List<MstEntry>();

        // Get full keys for proper reconstruction
        var leftFullKeys = MstEntry.GetFullKeys(leftEntries);
        var rightFullKeys = MstEntry.GetFullKeys(rightEntries);

        // Add all entries from left node
        for (int i = 0; i < leftEntries.Count; i++)
        {
            var entry = leftEntries[i];
            var newEntry = new MstEntry
            {
                KeySuffix = leftFullKeys[i], // Use full key, will be fixed later
                PrefixLength = 0,
                RecordCid = entry.RecordCid,
                TreeMstNodeCid = entry.TreeMstNodeCid,
                EntryIndex = mergedEntries.Count,
                NodeObjectId = mergedNode.NodeObjectId
            };

            // For the last entry from left, update its tree pointer to the merged middle subtree
            if (i == leftEntries.Count - 1)
            {
                newEntry.TreeMstNodeCid = mergedMiddleSubtree;
            }

            mergedEntries.Add(newEntry);
        }

        // Special case: if left has no entries, the merged middle subtree becomes the LeftMstNodeCid
        if (leftEntries.Count == 0)
        {
            mergedNode.LeftMstNodeCid = mergedMiddleSubtree;
        }

        // Add all entries from right node  
        for (int i = 0; i < rightEntries.Count; i++)
        {
            var entry = rightEntries[i];
            var newEntry = new MstEntry
            {
                KeySuffix = rightFullKeys[i], // Use full key, will be fixed later
                PrefixLength = 0,
                RecordCid = entry.RecordCid,
                TreeMstNodeCid = entry.TreeMstNodeCid,
                EntryIndex = mergedEntries.Count,
                NodeObjectId = mergedNode.NodeObjectId
            };
            mergedEntries.Add(newEntry);
        }

        // Fix up indexes and prefix lengths
        MstEntry.FixEntryIndexes(mergedEntries);
        MstEntry.FixPrefixLengths(mergedEntries);

        // Compute CID and save
        mergedNode.RecomputeCid(mergedEntries);
        _db.InsertMstNode(mergedNode);
        
        // Insert entries
        _db.InsertMstEntries((Guid)mergedNode.NodeObjectId!, mergedEntries);

        updatedNodeObjectIds.Add((Guid)mergedNode.NodeObjectId!);

        _logger.LogTrace($"MstDb.MergeMstNodes: Created merged node with CID {mergedNode.Cid?.Base32} and {mergedEntries.Count} entries");

        return mergedNode.Cid;
    }

    #endregion


    #region WALK

    public List<Guid> WalkEntry(string recordKey)
    {
        _logger.LogTrace($"MstDb.WalkEntry: Walking key: {recordKey}");

        //
        // Load from db
        //
        var repoCommit = _db.GetRepoCommit();
        var mstNodeRoot = _db.GetMstNodeByCid(repoCommit!.RootMstNodeCid!);
        var mstNodeRootEntries = _db.GetMstEntriesForNodeObjectId((Guid)mstNodeRoot!.NodeObjectId!);
        var originalRootMstNodeCid = repoCommit!.RootMstNodeCid!;

        //
        // Calculate root layer
        //
        // (note: I had this backwards at first. The correct way to represent the
        // tree is to have the "largest" key depths towards the top - at the root)
        //
        int rootLayer = GetRootLayer(mstNodeRoot, mstNodeRootEntries);

        //
        // Recursive walk
        //
        List<Guid> visitedNodeObjectIds = new List<Guid>();
        InternalWalkEntry(recordKey, mstNodeRoot!, mstNodeRootEntries, currentLayer: rootLayer, visitedNodeObjectIds);

        var newRootMstNodeCid = mstNodeRoot!.Cid!;

        //
        // Return
        //
        return visitedNodeObjectIds;
    }



    /// <summary>
    /// Internal recursive function to walk an entry in the MST.
    /// Caller passes in currentNode and currentEntries.
    /// On return, currentNode and currentEntries are updated in db.
    /// 
    /// Possible scenarios for one iteration:
    /// 
    ///     1) this level - visit existing MstEntry with identical key (visit)
    ///     2) next level - visit from MstNode.LeftMstNodeCid (go left)
    ///     3) next level - visit from MstEntry.TreeMstNodeCid (go right)
    /// 
    /// Note: currentLayer represents the MST layer where higher values are closer to root.
    /// Keys with depth == currentLayer belong at this node.
    /// Keys with depth < currentLayer go to child nodes (layer - 1).
    /// </summary>
    /// <param name="recordKey"></param>
    /// <param name="currentNode"></param>
    /// <param name="currentEntries"></param>
    /// <param name="currentLayer"></param>
    /// <param name="visitedNodeObjectIds"></param>
    private void InternalWalkEntry(string recordKey, MstNode? currentNode, List<MstEntry> currentEntries, int currentLayer, List<Guid> visitedNodeObjectIds)
    {
        if(currentNode is null)
        {
            return;
        }

        //
        // Calculate full keys and depth.
        //
        var currentEntryKeys = MstEntry.GetFullKeys(currentEntries);
        int keyDepthToVisit = MstEntry.GetKeyDepth(recordKey);

        _logger.LogTrace($"MstDb.InternalWalkEntry: currentLayer={currentLayer}, keyDepthToVisit={keyDepthToVisit}, currentNodeCid={currentNode.Cid?.Base32}");

        //
        // Visit from this level?
        // Key belongs here if its depth equals the current layer.
        //
        if(keyDepthToVisit == currentLayer)
        {
            //
            // Find entry to visit
            //
            int visitIndex = -1;
            for(int i = 0; i < currentEntries.Count; i++)
            {
                var entry = currentEntries[i];
                string fullKey = currentEntryKeys[i];
                _logger.LogTrace($"     MstDb.InternalWalkEntry: Checking entry {i}, fullKey={fullKey}, recordKey={recordKey}");
                int comparison = MstEntry.CompareKeys(recordKey, fullKey);

                if(comparison == 0)
                {
                    visitIndex = i;
                    break;
                }
            }

            _logger.LogTrace($"MstDb.InternalWalkEntry: visitIndex={visitIndex}");
            //
            // If found, visit it
            //
            if(visitIndex != -1)
            {
                visitedNodeObjectIds.Add((Guid) currentNode.NodeObjectId!);
            }

            return;
        }
        //
        // Else: Need to go to next level
        //
        else
        {
            //
            // Find visit position to traverse.
            //
            int visitPos = 0;
            for (int i = 0; i < currentEntries.Count; i++)
            {
                var entry = currentEntries[i];
                string entryKey = currentEntryKeys[i];
                
                if (MstEntry.CompareKeys(recordKey, entryKey) < 0)
                {
                    break;
                }
                
                visitPos = i + 1;
            }

            _logger.LogTrace($"MstDb.InternalWalkEntry: next level, visitPos={visitPos}");

            //
            // If 0, go left.
            //
            if(visitPos == 0)
            {
                //
                // Get left subtree
                //
                if (currentNode.LeftMstNodeCid != null)
                {
                    var leftNode = _db.GetMstNodeByCid(currentNode.LeftMstNodeCid);
                    if(leftNode is null) { return; }
                    var leftEntries = _db.GetMstEntriesForNodeObjectId((Guid)leftNode!.NodeObjectId!);

                    //
                    // Recurse - child layer is currentLayer - 1 (lower layers are deeper in tree)
                    //
                    InternalWalkEntry(recordKey, leftNode, leftEntries, currentLayer - 1, visitedNodeObjectIds);

                    //
                    // Add this object id
                    //
                    visitedNodeObjectIds.Add((Guid) currentNode.NodeObjectId!);
                }
                else
                {
                    _logger.LogTrace($"MstDb.InternalWalkEntry: LeftMstNodeCid is null, cannot go left.");
                }

                return;
            }
            //
            // Else, go right into the selected entry.
            //
            else
            {
                MstNode? rightNode = null;
                if (currentEntries[visitPos - 1].TreeMstNodeCid != null)
                {
                    rightNode = _db.GetMstNodeByCid(currentEntries[visitPos - 1].TreeMstNodeCid);
                }
                if(rightNode is null) 
                { 
                    _logger.LogTrace($"MstDb.InternalWalkEntry: Right subtree node is null, cannot go right.");
                    return; 
                }

                var rightEntries = _db.GetMstEntriesForNodeObjectId((Guid)rightNode!.NodeObjectId!);

                //
                // Recurse - child layer is currentLayer - 1 (lower layers are deeper in tree)
                //
                InternalWalkEntry(recordKey, rightNode, rightEntries, currentLayer - 1, visitedNodeObjectIds);
                //
                // Add this object id
                //
                visitedNodeObjectIds.Add((Guid) currentNode.NodeObjectId!);
                return;
            }
        }

    }

    #endregion
}