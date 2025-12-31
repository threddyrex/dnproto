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
    public (CidV1 originalRootMstNodeCid, CidV1 newRootMstNodeCid, List<CidV1> updatedCids) 
        PutEntry(string key, CidV1 recordCid)
    {
        //
        // Load from db
        //
        var repoCommit = _db.GetRepoCommit();
        var mstNodeRoot = _db.GetMstNode(repoCommit!.RootMstNodeCid!);
        var originalRootMstNodeCid = repoCommit!.RootMstNodeCid!;

        //
        // Recursive put
        //
        List<CidV1> updatedCids = new List<CidV1>();
        var newRootMstNodeCid = InternalPutEntry(key, recordCid, mstNodeRoot!, currentDepth: 0, updatedCids);

        //
        // Return
        //
        return (originalRootMstNodeCid, newRootMstNodeCid, updatedCids);
    }



    /// <summary>
    /// Internal recursive function to put an entry into the MST.
    /// Returns the *new* cid for currentNode (because cids change - they are a hash of the MstNode contents).
    /// 
    /// Possible scenarios for one iteration:
    /// 
    ///     1) this level - update existing MstEntry with identical key (update)
    ///     2) this level - insert new MstEntry (insert)
    ///     3) next level - insert into MstNode.LeftMstNodeCid (go left)
    ///     4) next level - insert into MstEntry.TreeMstNodeCid (go right)
    /// 
    /// </summary>
    private CidV1 InternalPutEntry(string key, CidV1 recordCid, MstNode currentNode, int currentDepth, List<CidV1> updatedCids)
    {

        //
        // Load entries from db
        //
        var entries = _db.GetMstEntriesForNode(currentNode.Cid!);
        var entryKeys = MstEntry.GetFullKeys(entries);


        //
        // INSERT AT THIS LEVEL?
        //
        int keyDepth = MstEntry.GetKeyDepth(key);
        if(keyDepth == currentDepth)
        {
            int insertIndex = 0;

            //
            // Loop through entries to find insert position.
            //
            for(int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string fullKey = entryKeys[i];
                int comparison = MstEntry.CompareKeys(key, fullKey);

                //
                // Key exists already - update it.
                //
                if(comparison == 0)
                {
                    entry.RecordCid = recordCid;


                    CidV1 oldCid1 = currentNode.Cid!;
                    currentNode.Cid = CidV1.ComputeCidForDagCbor(currentNode.ToDagCborObject(entries))!;

                    MstEntry.FixEntryNodeCids(entries, currentNode.Cid!);


                    //
                    // 🚨 DB UPDATE 🚨
                    //
                    updatedCids.Add(currentNode.Cid!);
                    _db.ReplaceMstNode(
                        oldCid1, // old cid
                        currentNode, // node (with new cid)
                        entries); // entries


                    return currentNode.Cid!;
                }
                else if(comparison < 0)
                {
                    // Insert new entry before this one
                    insertIndex = i;
                    break;
                }

                insertIndex++;
            }

            // create new entry
            var newEntry = new MstEntry
            {
                MstNodeCid = currentNode.Cid, // gets fixed later
                EntryIndex = insertIndex, // gets fixed later
                KeySuffix = key, // gets fixed later
                PrefixLength = 0, // gets fixed later
                TreeMstNodeCid = null,
                RecordCid = recordCid
            };

            entries.Insert(insertIndex, newEntry);
            entryKeys.Insert(insertIndex, key);

            // fix entry indices (0 -> n-1)
            MstEntry.FixEntryIndexes(entries);

            // fix prefix lengths
            MstEntry.FixPrefixLengths(entries);

            CidV1 oldCid2 = currentNode.Cid!;
            currentNode.Cid = CidV1.ComputeCidForDagCbor(currentNode.ToDagCborObject(entries))!;

            MstEntry.FixEntryNodeCids(entries, currentNode.Cid!);

            //
            // 🚨 DB UPDATE 🚨
            //
            updatedCids.Add(currentNode.Cid!);
            _db.ReplaceMstNode(
                oldCid2, // old cid
                currentNode, // node
                entries); // entries

            //
            // Return updated cid
            //
            return currentNode.Cid!;

        }
        //
        // ELSE: NEED TO GO TO NEXT LEVEL
        //
        // This could either mean:
        //      1 - MstNode.LeftMstNodeCid - the node's left sub tree
        //      2 - MstEntry.TreeMstNodeCid - one of the entries' right sub-tree.
        //
        else
        {
            //
            // Find insert position
            //
            int insertPos = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string entryKey = entryKeys[i];
                
                if (MstEntry.CompareKeys(key, entryKey) < 0)
                {
                    break;
                }
                
                insertPos = i + 1;
            }


            //
            // (insertPos == 0) means we need to go to left subtree (MstNode.LeftMstNodeCid)
            //
            if(insertPos == 0)
            {
                //
                // Get (or create) left subtree
                //
                MstNode? leftNode = null;
                if (currentNode.LeftMstNodeCid != null)
                {
                    leftNode = _db.GetMstNode(currentNode.LeftMstNodeCid);
                }
                else
                {
                    leftNode = new MstNode();
                    leftNode.Cid = CidV1.ComputeCidForDagCbor(leftNode.ToDagCborObject(new List<MstEntry>()))!;
                    _db.InsertMstNode(leftNode);
                    currentNode.LeftMstNodeCid = leftNode.Cid;
                }

                // Recurse
                CidV1 leftNodeNewCid = InternalPutEntry(key, recordCid, leftNode!, currentDepth + 1, updatedCids);


                //
                // Update currentNode (because its LeftMstNodeCid changed)
                // (can reuse entries because it hasn't changed)
                //
                CidV1 oldCid3 = currentNode.Cid!;
                currentNode.LeftMstNodeCid = leftNodeNewCid;
                currentNode.Cid = CidV1.ComputeCidForDagCbor(currentNode.ToDagCborObject(entries))!;
                MstEntry.FixEntryNodeCids(entries, currentNode.Cid!);

                //
                // 🚨 DB UPDATE 🚨
                //
                updatedCids.Add(currentNode.Cid!);
                _db.ReplaceMstNode(
                    oldCid3, // old cid
                    currentNode, // node
                    entries); // entries


                //
                // Return updated cid
                //
                return currentNode.Cid!;

            }
            //
            // (insertPos > 0) means go to right subtree of selected entry (MstEntry.TreeMstNodeCid)
            //
            else
            {
                // Go to right subtree of the selected entry
                MstEntry selectedEntry = entries[insertPos - 1];
                MstNode? rightNode = null;
                if (selectedEntry.TreeMstNodeCid != null)
                {
                    rightNode = _db.GetMstNode(selectedEntry.TreeMstNodeCid);
                }
                else
                {
                    //
                    // Create new right node
                    //
                    rightNode = new MstNode();
                    rightNode.Cid = CidV1.ComputeCidForDagCbor(rightNode.ToDagCborObject(new List<MstEntry>()))!;
                    _db.InsertMstNode(rightNode);
                    selectedEntry.TreeMstNodeCid = rightNode.Cid;
                }
                
                // Recurse
                CidV1 newRightNodeCid = InternalPutEntry(key, recordCid, rightNode!, currentDepth + 1, updatedCids);

                //
                // Update currentNode (because selectedEntry.TreeMstNodeCid changed)
                //
                selectedEntry.TreeMstNodeCid = newRightNodeCid;
                CidV1 oldCid4 = currentNode.Cid!;
                currentNode.Cid = CidV1.ComputeCidForDagCbor(currentNode.ToDagCborObject(entries))!;
                MstEntry.FixEntryNodeCids(entries, currentNode.Cid!);

                //
                // 🚨 DB UPDATE 🚨
                //
                updatedCids.Add(currentNode.Cid!);
                _db.ReplaceMstNode(
                    oldCid4, // old cid
                    currentNode, // node
                    entries); // entries

                return currentNode.Cid!;
            }
        }
    }
    #endregion



}