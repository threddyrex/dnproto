using dnproto.mst;
using dnproto.pds;
using dnproto.repo;

namespace dnproto.cli.commands;

/// <summary>
/// Reads the MST from the local pds.db and prints the entire tree
/// in DAG-CBOR "tree" format, showing every node and its entries.
/// </summary>
public class PrintDbMst : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { });
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[] { "format" });
    }

    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string format = CommandLineInterface.GetArgumentValue(arguments, "format") ?? "tree";

        //
        // Connect to pds db
        //
        PdsDb db;
        try
        {
            db = PdsDb.ConnectPdsDb(LocalFileSystem!, Logger);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to connect to PDS database: {ex.Message}");
            return;
        }

        //
        // Get repo header and commit
        //
        RepoCommit repoCommit;
        try
        {
            repoCommit = db.GetRepoCommit();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to get repo commit: {ex.Message}");
            return;
        }

        //
        // Build MST from all records
        //
        List<MstItem> mstItems = db.GetAllRepoRecordMstItems();
        Mst mst = Mst.AssembleTreeFromItems(mstItems);
        List<MstNode> allNodes = mst.FindAllNodes();

        //
        // Convert all MST nodes to DAG-CBOR
        //
        Dictionary<MstNode, (CidV1, DagCborObject)> mstNodeCache = new Dictionary<MstNode, (CidV1, DagCborObject)>();
        foreach (MstNode node in allNodes)
        {
            RepoMst.ConvertMstNodeToDagCbor(mstNodeCache, node);
        }

        //
        // Stats
        //
        int mstEntryCount = 0;
        foreach (var node in allNodes)
        {
            mstEntryCount += node.Entries.Count;
        }

        Logger.LogInfo("");
        Logger.LogInfo("=== PRINT DB MST ===");
        Logger.LogInfo("");
        Logger.LogInfo($"Commit CID:        {repoCommit.Cid?.GetBase32() ?? "<null>"}");
        Logger.LogInfo($"Root MST Node CID: {repoCommit.RootMstNodeCid?.GetBase32() ?? "<null>"}");
        Logger.LogInfo($"Rev:               {repoCommit.Rev}");
        Logger.LogInfo($"mstItems.Count:    {mstItems.Count}");
        Logger.LogInfo($"allNodes.Count:    {allNodes.Count}");
        Logger.LogInfo($"mstEntryCount:     {mstEntryCount}");
        Logger.LogInfo($"root depth:        {mst.Root.KeyDepth}");
        Logger.LogInfo("");

        //
        // Print based on format
        //
        if (format.Equals("tree", StringComparison.OrdinalIgnoreCase))
        {
            PrintTreeFormat(mstNodeCache, mst.Root, 0, "root");
        }
        else
        {
            Logger.LogError($"Unknown format: {format}. Use 'tree'.");
        }
    }

    private void PrintTreeFormat(
        Dictionary<MstNode, (CidV1, DagCborObject)> mstNodeCache,
        MstNode node,
        int indent,
        string direction)
    {
        if (!mstNodeCache.TryGetValue(node, out var cached))
        {
            Logger.LogInfo($"{new string(' ', indent)}[{direction}] [depth={node.KeyDepth}] [NOT IN CACHE]");
            return;
        }

        var (nodeCid, nodeDagCbor) = cached;
        byte[] nodeBytes = nodeDagCbor.ToBytes();

        Logger.LogInfo($"{new string(' ', indent)}[{direction}] [depth={node.KeyDepth}] {nodeCid.GetBase32()}");
        Logger.LogInfo($"{new string(' ', indent)}  Hex: {BitConverter.ToString(nodeBytes).Replace("-", "")}");
        Logger.LogInfo($"{new string(' ', indent)}  DAG-CBOR:");
        Logger.LogInfo(DagCborObject.GetRecursiveDebugString(nodeDagCbor, (indent / 2) + 2));

        foreach (var entry in node.Entries)
        {
            Logger.LogInfo($"{new string(' ', indent)}  {entry.Key}: {entry.Value}");
        }

        Logger.LogInfo("");

        if (node.LeftTree != null)
        {
            PrintTreeFormat(mstNodeCache, node.LeftTree, indent + 2, "left");
        }

        foreach (var entry in node.Entries)
        {
            if (entry.RightTree != null)
            {
                PrintTreeFormat(mstNodeCache, entry.RightTree, indent + 2, "right");
            }
        }
    }
}
