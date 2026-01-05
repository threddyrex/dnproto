using System.Security.Cryptography;
using System.Text;
using dnproto.pds.db;
using dnproto.repo;

namespace dnproto.pds;


/// <summary>
/// MST implementation, backed by database.
/// 
/// Instance methods usually modify the MST stored in the database (_db).
/// 
/// </summary>
public class Mst
{
    private PdsDb _db;

    public Mst(PdsDb db)
    {
        _db = db;
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
    /// Caller passes in MstNode and MstEntries. 
    /// On return, MstNode and MstEntries are updated in db.
    /// 
    /// Possible scenarios for one iteration:
    /// 
    ///     1) this level - update existing MstEntry with identical key (update)
    ///     2) this level - insert new MstEntry (insert)
    ///     3) next level - insert into MstNode.LeftMstNodeCid (go left)
    ///     4) next level - insert into MstEntry.TreeMstNodeCid (go right)
    /// 
    /// </summary>
    private void InternalPutEntry(string key, CidV1 recordCid, MstNode mstNode, List<MstEntry> mstEntries, int currentDepth, List<Guid> updatedNodeObjectIds)
    {
        //
        // Prep
        //
        var entryKeys = MstEntry.GetFullKeys(mstEntries);
        int keyDepth = MstEntry.GetKeyDepth(key);

        //
        // Insert at this level?
        //
        if(keyDepth == currentDepth)
        {
            int insertIndex = 0;

            //
            // Loop through entries to find insert position.
            //
            for(int i = 0; i < mstEntries.Count; i++)
            {
                var entry = mstEntries[i];
                string fullKey = entryKeys[i];
                int comparison = MstEntry.CompareKeys(key, fullKey);

                //
                // CASE 1 - update existing MstEntry with identical key (update)
                //
                if(comparison == 0)
                {
                    entry.RecordCid = recordCid;
                    mstNode.RecomputeCid(mstEntries);
                    updatedNodeObjectIds.Add((Guid) mstNode.NodeObjectId!);
                    _db.ReplaceMstNode(mstNode, mstEntries);
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
                EntryIndex = insertIndex, // gets fixed later
                KeySuffix = key, // gets fixed later
                PrefixLength = 0, // gets fixed later
                TreeMstNodeCid = null,
                RecordCid = recordCid
            };

            mstEntries.Insert(insertIndex, newEntry);
            entryKeys.Insert(insertIndex, key);
            MstEntry.FixEntryIndexes(mstEntries);
            MstEntry.FixPrefixLengths(mstEntries);
            mstNode.RecomputeCid(mstEntries);
            updatedNodeObjectIds.Add((Guid) mstNode.NodeObjectId!);
            _db.ReplaceMstNode(mstNode, mstEntries);
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
            for (int i = 0; i < mstEntries.Count; i++)
            {
                var entry = mstEntries[i];
                string entryKey = entryKeys[i];
                
                if (MstEntry.CompareKeys(key, entryKey) < 0)
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
                if (mstNode.LeftMstNodeCid != null)
                {
                    leftNode = _db.GetMstNodeByCid(mstNode.LeftMstNodeCid);
                }
                else
                {
                    leftNode = new MstNode();
                    leftNode.NodeObjectId = Guid.NewGuid();
                    leftNode.RecomputeCid(new List<MstEntry>());
                    _db.InsertMstNode(leftNode);
                    mstNode.LeftMstNodeCid = leftNode.Cid;
                }

                if(leftNode is null) 
                {
                    throw new Exception("Left MST node is null after creation.");
                }

                List<MstEntry> leftEntries = _db.GetMstEntriesForNodeObjectId((Guid)leftNode.NodeObjectId!);


                //
                // Recurse
                //
                InternalPutEntry(key, recordCid, leftNode!, leftEntries, currentDepth + 1, updatedNodeObjectIds);


                //
                // Update currentNode (because its LeftMstNodeCid changed)
                // (can reuse entries because it hasn't changed)
                //
                mstNode.LeftMstNodeCid = leftNode.Cid;
                mstNode.RecomputeCid(mstEntries);
                updatedNodeObjectIds.Add((Guid) mstNode.NodeObjectId!);
                _db.ReplaceMstNode(mstNode, mstEntries);


                //
                // Return
                //
                return;

            }
            //
            // CASE 4 - insert into MstEntry.TreeMstNodeCid (go right)
            //
            else
            {
                // Go to right subtree of the selected entry
                MstEntry selectedEntry = mstEntries[insertPos - 1];
                MstNode? rightNode = null;
                if (selectedEntry.TreeMstNodeCid != null)
                {
                    rightNode = _db.GetMstNodeByCid(selectedEntry.TreeMstNodeCid);
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
                    selectedEntry.TreeMstNodeCid = rightNode.Cid;
                }

                List<MstEntry> rightEntries = _db.GetMstEntriesForNodeObjectId((Guid)rightNode?.NodeObjectId!);

                //                
                // Recurse
                //
                InternalPutEntry(key, recordCid, rightNode!, rightEntries, currentDepth + 1, updatedNodeObjectIds);

                //
                // Update currentNode (because selectedEntry.TreeMstNodeCid changed)
                //
                selectedEntry.TreeMstNodeCid = rightNode.Cid;
                mstNode.RecomputeCid(mstEntries);
                updatedNodeObjectIds.Add((Guid) mstNode.NodeObjectId!);
                _db.ReplaceMstNode(mstNode, mstEntries);
                return;
            }
        }
    }
    #endregion



}