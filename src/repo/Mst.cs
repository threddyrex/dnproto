using System.Reflection.Metadata.Ecma335;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace dnproto.repo;

/// <summary>
/// MST functions.
/// </summary>
public class Mst
{
    /// <summary>
    /// Walks MST in a repo file, in tree node order.
    /// Requires reading entire repo file into memory.
    /// </summary>
    /// <param name="repoFile"></param>
    /// <param name="mstNodeCallback"></param>
    public static void WalkMst(string repoFile,
        Func<RepoHeader, RepoCommit, Dictionary<CidV1, MstNode>, Dictionary<CidV1, List<MstEntry>>, HashSet<CidV1>, bool> dataLoadedCallback,
        Func<string, MstNode, int, List<MstEntry>, bool> mstNodeCallback,
        Func<string, bool> errorCallback)
    {
        if (string.IsNullOrEmpty(repoFile)) return;
        using (var fs = new FileStream(repoFile, FileMode.Open))
        {
            WalkMst(fs, dataLoadedCallback, mstNodeCallback, errorCallback);
        }
    }

    public static void WalkMst(Stream s,
        Func<RepoHeader, RepoCommit, Dictionary<CidV1, MstNode>, Dictionary<CidV1, List<MstEntry>>, HashSet<CidV1>, bool> dataLoadedCallback,
        Func<string, MstNode, int, List<MstEntry>, bool> mstNodeCallback,
        Func<string, bool> errorCallback)
    {
        //
        // Check stream.
        //
        if (s == null) return;
        if (s.Length == 0) return;


        //
        // Read entire repo into memory (minus the AtProto record blocks).
        //
        RepoHeader? repoHeader = null;
        RepoCommit? repoCommit = null;
        Dictionary<CidV1, MstNode> mstNodes = new Dictionary<CidV1, MstNode>();
        Dictionary<CidV1, List<MstEntry>> mstNodeEntries = new Dictionary<CidV1, List<MstEntry>>();
        HashSet<CidV1> atProtoRecordCids = new HashSet<CidV1>();

        Repo.WalkRepo(s,
            (header) =>
            {
                repoHeader = header;
                return true;
            },
            (record) =>
            {
                if(record.IsRepoCommit())
                {
                    if(repoCommit != null)
                    {
                        errorCallback("Multiple Repo Commit records found in repo.");
                    }
                    else
                    {
                        repoCommit = record.ToRepoCommit();
                        if (repoCommit == null)
                        {
                            errorCallback("Failed to parse Repo Commit record.");
                        }
                    }
                }
                else if(record.IsMstNode())
                {
                    (MstNode? mstNode, List<MstEntry>? mstEntries) = record.ToMstNode();
                    if(mstNode != null && mstNode.Cid != null && mstEntries != null)
                    {
                        mstNodes[mstNode.Cid] = mstNode;
                        mstNodeEntries[mstNode.Cid] = mstEntries;
                    }
                    else
                    {
                        errorCallback($"Failed to parse MST node record. {mstNode?.Cid}");
                    }
                }
                else if(record.IsAtProtoRecord())
                {
                    atProtoRecordCids.Add(record.Cid);
                }
                else
                {
                    errorCallback($"Unknown record type found in repo: {record.Cid}");
                }


                return true;
            });

        //
        // Send data back to caller.
        //
        bool continueWalk = dataLoadedCallback(repoHeader!, repoCommit!, mstNodes, mstNodeEntries, atProtoRecordCids);
        if(!continueWalk)
        {
            return;
        }


        //
        // Start at the root node and walk the MST.
        //
        MstNode? rootNode = null;
        if(repoCommit != null && repoCommit.RootMstNodeCid != null)
        {
            CidV1 rootCid = (CidV1)repoCommit.RootMstNodeCid;
            if(mstNodes.ContainsKey(rootCid))
            {
                rootNode = mstNodes[rootCid];
            }
            else
            {
                errorCallback("Root MST node not found in repo.");
                return;
            }
        }
        else
        {
            errorCallback("Repo Commit or Root MST Node CID is null.");
            return;
        }

        VisitNode("(root) ", rootNode, 0, mstNodes, mstNodeEntries, mstNodeCallback, errorCallback);
    }

    private static bool VisitNode(string direction, MstNode currentNode, 
        int currentDepth,
        Dictionary<CidV1, MstNode> allMstNodes, 
        Dictionary<CidV1, List<MstEntry>> allMstNodeEntries, 
        Func<string, MstNode, int, List<MstEntry>, bool> mstNodeCallback, 
        Func<string, bool> errorCallback)
    {
        if(currentNode is null || currentNode.Cid is null)
        {
            errorCallback("Current MST Node or its CID is null.");
            return false;
        }

        // Get entries for this node
        if(!allMstNodeEntries.ContainsKey(currentNode.Cid))
        {
            errorCallback($"MST Node entries not found for node: {currentNode.Cid}");
            return false;
        }

        var entries = allMstNodeEntries[currentNode.Cid];

        // Call the callback
        bool continueWalk = mstNodeCallback(direction, currentNode, currentDepth, entries);
        if(!continueWalk)
        {
            return false;
        }

        // Visit left
        if(currentNode.LeftMstNodeCid != null)
        {
            if(allMstNodes.ContainsKey(currentNode.LeftMstNodeCid))
            {
                var leftNode = allMstNodes[currentNode.LeftMstNodeCid];
                continueWalk = VisitNode("(left) ", leftNode, currentDepth + 1, allMstNodes, allMstNodeEntries, mstNodeCallback, errorCallback);
                if(!continueWalk)
                {
                    return false;
                }
            }
            else
            {
                errorCallback($"Left Child MST Node not found: {currentNode.LeftMstNodeCid}");
            }
        }

        // Visit right
        foreach(var entry in entries)
        {
            if(entry.TreeMstNodeCid != null)
            {
                if(allMstNodes.ContainsKey(entry.TreeMstNodeCid))
                {
                    var rightNode = allMstNodes[entry.TreeMstNodeCid];
                    continueWalk = VisitNode("(right) ", rightNode, currentDepth + 1, allMstNodes, allMstNodeEntries, mstNodeCallback, errorCallback);
                    if(!continueWalk)
                    {
                        return false;
                    }
                }
                else
                {
                    errorCallback($"Child MST Node not found: {entry.TreeMstNodeCid}");
                }
            }
        }

        return true;
    }
}

