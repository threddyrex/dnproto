
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
        // Recursive put
        //
        List<Guid> updatedNodeObjectIds = new List<Guid>();
        InternalPutEntry(key, recordCid, mstNodeRoot!, mstNodeRootEntries, currentDepth: 0, updatedNodeObjectIds);

        var newRootMstNodeCid = mstNodeRoot!.Cid!;

        //
        // Return
        //
        return (originalRootMstNodeCid, newRootMstNodeCid, updatedNodeObjectIds);
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
    /// </summary>
    private void InternalPutEntry(string recordKeyToInsert, CidV1 recordCidToInsert, MstNode currentNode, List<MstEntry> currentEntries, int currentDepth, List<Guid> updatedNodeObjectIds)
    {
        //
        // Calculate full keys and depth.
        //
        var currentEntryKeys = MstEntry.GetFullKeys(currentEntries);
        int keyDepthToInsert = MstEntry.GetKeyDepth(recordKeyToInsert);

        //
        // Insert at this level?
        //
        if(keyDepthToInsert == currentDepth)
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
                // Recurse
                //
                InternalPutEntry(recordKeyToInsert, recordCidToInsert, leftNode!, leftEntries, currentDepth + 1, updatedNodeObjectIds);


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
                // Recurse
                //
                InternalPutEntry(recordKeyToInsert, recordCidToInsert, rightNode!, rightEntries, currentDepth + 1, updatedNodeObjectIds);

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
        // Recursive put
        //
        List<Guid> updatedNodeObjectIds = new List<Guid>();
        InternalDeleteEntry(recordKeyToDelete, mstNodeRoot!, mstNodeRootEntries, currentDepth: 0, updatedNodeObjectIds);

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
    /// </summary>
    /// <param name="recordKeyToDelete"></param>
    /// <param name="currentNode"></param>
    /// <param name="currentEntries"></param>
    /// <param name="currentDepth"></param>
    /// <param name="updatedNodeObjectIds"></param>
    private void InternalDeleteEntry(string recordKeyToDelete, MstNode? currentNode, List<MstEntry> currentEntries, int currentDepth, List<Guid> updatedNodeObjectIds)
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

        _logger.LogTrace($"MstDb.InternalDeleteEntry: currentDepth={currentDepth}, keyDepthToDelete={keyDepthToDelete}, currentNodeCid={currentNode.Cid?.Base32}");

        //
        // Delete from this level?
        //
        if(keyDepthToDelete == currentDepth)
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
                    // Recurse
                    //
                    InternalDeleteEntry(recordKeyToDelete, leftNode, leftEntries, currentDepth + 1, updatedNodeObjectIds);

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
                // Recurse.
                //
                InternalDeleteEntry(recordKeyToDelete, rightNode, rightEntries, currentDepth + 1, updatedNodeObjectIds);

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


    #endregion
}