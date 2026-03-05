using System.Text.Json;
using dnproto.cli;
using dnproto.mst;
using dnproto.pds;
using dnproto.repo;

namespace dnproto.cli.commands;

/// <summary>
/// Gets a record directly from the local pds.db and prints its details.
/// This is a "sync get record" operation that reads from the local database
/// rather than making any network calls. It builds the same MST proof chain
/// that the real com.atproto.sync.getRecord endpoint would return.
/// </summary>
public class SyncGetRecordLocal : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "collection", "rkey" });
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[] { "format" });
    }

    private Dictionary<MstNode, (CidV1, DagCborObject)>? _mstNodeCache;

    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? collection = CommandLineInterface.GetArgumentValue(arguments, "collection");
        string? rkey = CommandLineInterface.GetArgumentValue(arguments, "rkey");
        string? format = CommandLineInterface.GetArgumentValue(arguments, "format") ?? "dagcbor";

        if (string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(rkey))
        {
            Logger.LogError("collection and rkey arguments are required.");
            return;
        }

        string fullKey = $"{collection}/{rkey}";

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
        RepoHeader repoHeader;
        RepoCommit repoCommit;
        try
        {
            repoHeader = db.GetRepoHeader();
            repoCommit = db.GetRepoCommit();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to get repo header/commit: {ex.Message}");
            return;
        }

        //
        // Get record
        //
        RepoRecord repoRecord;
        try
        {
            repoRecord = db.GetRepoRecord(collection, rkey);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Record not found: {collection}/{rkey}");
            Logger.LogTrace($"Error: {ex.Message}");
            return;
        }

        //
        // Build MST from all records
        //
        Mst mst = Mst.AssembleTreeFromItems(db.GetAllRepoRecordMstItems());
        List<MstNode> mstNodes = mst.FindNodesForKey(fullKey);
        List<MstNode> allNodes = mst.FindAllNodes();

        //
        // Convert all MST nodes to DAG-CBOR (needed to compute CIDs correctly)
        //
        Dictionary<MstNode, (CidV1, DagCborObject)> mstNodeCache = new Dictionary<MstNode, (CidV1, DagCborObject)>();
        foreach (MstNode node in allNodes)
        {
            RepoMst.ConvertMstNodeToDagCbor(mstNodeCache, node);
        }
        _mstNodeCache = mstNodeCache;

        string userDid = db.GetConfigProperty("UserDid");

        //
        // Print based on format
        //
        if (format.Equals("dagcbor", StringComparison.OrdinalIgnoreCase))
        {
            PrintDagCborFormat(repoHeader, repoCommit, mstNodes, mstNodeCache, repoRecord, collection, rkey, userDid);
        }
        else if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            PrintJsonFormat(repoHeader, repoCommit, mstNodes, mstNodeCache, repoRecord, collection, rkey, userDid);
        }
        else if (format.Equals("raw", StringComparison.OrdinalIgnoreCase))
        {
            PrintRawFormat(repoHeader, repoCommit, mstNodes, mstNodeCache, repoRecord, collection, rkey, userDid);
        }
        else if (format.Equals("tree", StringComparison.OrdinalIgnoreCase))
        {
            PrintTreeFormat(repoHeader, repoCommit, mstNodes, mstNodeCache, repoRecord, collection, rkey, userDid);
        }
        else
        {
            Logger.LogError($"Unknown format: {format}. Use 'dagcbor', 'json', 'raw', or 'tree'.");
        }
    }

    private void PrintDagCborFormat(
        RepoHeader repoHeader,
        RepoCommit repoCommit,
        List<MstNode> mstNodes,
        Dictionary<MstNode, (CidV1, DagCborObject)> mstNodeCache,
        RepoRecord repoRecord,
        string collection,
        string rkey,
        string userDid)
    {
        Logger.LogInfo("");
        Logger.LogInfo("=== SYNC GET RECORD (DAG-CBOR FORMAT) ===");
        Logger.LogInfo($"AT URI: at://{userDid}/{collection}/{rkey}");
        Logger.LogInfo("");

        // CAR Header
        Logger.LogInfo("--- BLOCK 1: CAR HEADER ---");
        var headerDagCbor = repoHeader.ToDagCborObject();
        byte[] headerBytes = headerDagCbor.ToBytes();
        Logger.LogInfo($"CID:    {repoHeader.RepoCommitCid?.GetBase32() ?? "<null>"} (root reference)");
        Logger.LogInfo($"Length: {headerBytes.Length} bytes");
        Logger.LogInfo($"Hex:    {BitConverter.ToString(headerBytes).Replace("-", "")}");
        Logger.LogInfo("");

        // Repo Commit
        Logger.LogInfo("--- BLOCK 2: REPO COMMIT ---");
        var commitDagCbor = repoCommit.ToDagCborObject();
        byte[] commitBytes = commitDagCbor.ToBytes();
        Logger.LogInfo($"CID:    {repoCommit.Cid?.GetBase32() ?? "<null>"}");
        Logger.LogInfo($"Length: {commitBytes.Length} bytes");
        Logger.LogInfo($"Hex:    {BitConverter.ToString(commitBytes).Replace("-", "")}");
        Logger.LogInfo("");

        // MST Nodes (proof chain)
        int blockNum = 3;
        Logger.LogInfo($"--- BLOCKS {blockNum}-{blockNum + mstNodes.Count - 1}: MST NODES (PROOF CHAIN) ---");
        Logger.LogInfo($"Total MST nodes in proof chain: {mstNodes.Count}");
        Logger.LogInfo("");

        foreach (MstNode node in mstNodes)
        {
            var (nodeCid, nodeDagCbor) = mstNodeCache[node];
            byte[] nodeBytes = nodeDagCbor.ToBytes();
            Logger.LogInfo($"  BLOCK {blockNum}: MST NODE");
            Logger.LogInfo($"  CID:    {nodeCid.GetBase32()}");
            Logger.LogInfo($"  Length: {nodeBytes.Length} bytes");
            Logger.LogInfo($"  Hex:    {BitConverter.ToString(nodeBytes).Replace("-", "")}");
            Logger.LogInfo("");
            blockNum++;
        }

        // Record
        Logger.LogInfo($"--- BLOCK {blockNum}: RECORD ---");
        byte[] recordBytes = repoRecord.DataBlock.ToBytes();
        Logger.LogInfo($"CID:    {repoRecord.Cid?.GetBase32() ?? "<null>"}");
        Logger.LogInfo($"$type:  {repoRecord.AtProtoType ?? "<null>"}");
        Logger.LogInfo($"Length: {recordBytes.Length} bytes");
        Logger.LogInfo($"Hex:    {BitConverter.ToString(recordBytes).Replace("-", "")}");
    }

    private void PrintJsonFormat(
        RepoHeader repoHeader,
        RepoCommit repoCommit,
        List<MstNode> mstNodes,
        Dictionary<MstNode, (CidV1, DagCborObject)> mstNodeCache,
        RepoRecord repoRecord,
        string collection,
        string rkey,
        string userDid)
    {
        Logger.LogInfo("");
        Logger.LogInfo("=== SYNC GET RECORD (JSON FORMAT) ===");
        Logger.LogInfo($"AT URI: at://{userDid}/{collection}/{rkey}");
        Logger.LogInfo("");

        // CAR Header
        Logger.LogInfo("--- CAR HEADER ---");
        var headerDagCbor = repoHeader.ToDagCborObject();
        Logger.LogInfo(JsonSerializer.Serialize(headerDagCbor.GetRawValue(), new JsonSerializerOptions { WriteIndented = true }));
        Logger.LogInfo("");

        // Repo Commit
        Logger.LogInfo("--- REPO COMMIT ---");
        var commitDagCbor = repoCommit.ToDagCborObject();
        Logger.LogInfo(JsonSerializer.Serialize(commitDagCbor.GetRawValue(), new JsonSerializerOptions { WriteIndented = true }));
        Logger.LogInfo("");

        // MST Nodes
        Logger.LogInfo($"--- MST NODES (PROOF CHAIN: {mstNodes.Count} nodes) ---");
        int nodeNum = 1;
        foreach (MstNode node in mstNodes)
        {
            var (nodeCid, nodeDagCbor) = mstNodeCache[node];
            Logger.LogInfo($"MST NODE {nodeNum} (CID: {nodeCid.GetBase32()}):");
            Logger.LogInfo(JsonSerializer.Serialize(nodeDagCbor.GetRawValue(), new JsonSerializerOptions { WriteIndented = true }));
            Logger.LogInfo("");
            nodeNum++;
        }

        // Record
        Logger.LogInfo("--- RECORD ---");
        Logger.LogInfo($"CID:   {repoRecord.Cid?.GetBase32() ?? "<null>"}");
        Logger.LogInfo($"$type: {repoRecord.AtProtoType ?? "<null>"}");
        Logger.LogInfo(JsonSerializer.Serialize(repoRecord.DataBlock.GetRawValue(), new JsonSerializerOptions { WriteIndented = true }));
    }

    private void PrintRawFormat(
        RepoHeader repoHeader,
        RepoCommit repoCommit,
        List<MstNode> mstNodes,
        Dictionary<MstNode, (CidV1, DagCborObject)> mstNodeCache,
        RepoRecord repoRecord,
        string collection,
        string rkey,
        string userDid)
    {
        Logger.LogInfo("");
        Logger.LogInfo("=== SYNC GET RECORD (RAW FORMAT) ===");
        Logger.LogInfo($"AT URI: at://{userDid}/{collection}/{rkey}");
        Logger.LogInfo("");

        Logger.LogInfo($"Record CID:        {repoRecord.Cid?.GetBase32() ?? "<null>"}");
        Logger.LogInfo($"$type:             {repoRecord.AtProtoType ?? "<null>"}");
        Logger.LogInfo($"Commit CID:        {repoCommit.Cid?.GetBase32() ?? "<null>"}");
        Logger.LogInfo($"Root MST Node CID: {repoCommit.RootMstNodeCid?.GetBase32() ?? "<null>"}");
        Logger.LogInfo($"MST Proof Chain:   {mstNodes.Count} nodes");
        Logger.LogInfo("");

        byte[] recordBytes = repoRecord.DataBlock.ToBytes();
        Logger.LogInfo($"Record Length: {recordBytes.Length} bytes");
        Logger.LogInfo($"Record Hex:    {BitConverter.ToString(recordBytes).Replace("-", "")}");
    }

    private void PrintTreeFormat(
        RepoHeader repoHeader,
        RepoCommit repoCommit,
        List<MstNode> mstNodes,
        Dictionary<MstNode, (CidV1, DagCborObject)> mstNodeCache,
        RepoRecord repoRecord,
        string collection,
        string rkey,
        string userDid)
    {
        Logger.LogInfo("");
        Logger.LogInfo("=== SYNC GET RECORD (TREE FORMAT) ===");
        Logger.LogInfo($"AT URI: at://{userDid}/{collection}/{rkey}");
        Logger.LogInfo("");

        // Repo Commit
        Logger.LogInfo("--- REPO COMMIT ---");
        Logger.LogInfo($"CID:              {repoCommit.Cid?.GetBase32() ?? "<null>"}");
        Logger.LogInfo($"Root MST Node:    {repoCommit.RootMstNodeCid?.GetBase32() ?? "<null>"}");
        Logger.LogInfo($"Rev:              {repoCommit.Rev}");
        Logger.LogInfo($"Version:          {repoCommit.Version}");
        Logger.LogInfo("");

        // MST Proof Chain
        Logger.LogInfo("--- MST PROOF CHAIN ---");
        Logger.LogInfo($"Total nodes in proof: {mstNodes.Count}");
        Logger.LogInfo("");

        for (int nodeIdx = 0; nodeIdx < mstNodes.Count; nodeIdx++)
        {
            var node = mstNodes[nodeIdx];
            if (!mstNodeCache.TryGetValue(node, out var cached))
            {
                Logger.LogInfo($"[NODE {nodeIdx} NOT IN CACHE]");
                continue;
            }

            var (nodeCid, nodeDagCbor) = cached;
            byte[] nodeBytes = nodeDagCbor.ToBytes();

            Logger.LogInfo($"NODE {nodeIdx} (depth={node.KeyDepth})");
            Logger.LogInfo($"  CID: {nodeCid.GetBase32()}");
            Logger.LogInfo($"  Hex: {BitConverter.ToString(nodeBytes).Replace("-", "")}");
            Logger.LogInfo($"  DAG-CBOR:");
            Logger.LogInfo(DagCborObject.GetRecursiveDebugString(nodeDagCbor, 2));
            Logger.LogInfo("");
        }

        // Target Record
        Logger.LogInfo("--- TARGET RECORD ---");
        Logger.LogInfo($"Key:   {collection}/{rkey}");
        Logger.LogInfo($"CID:   {repoRecord.Cid?.GetBase32() ?? "<null>"}");
        Logger.LogInfo($"$type: {repoRecord.AtProtoType ?? "<null>"}");
        byte[] recordBytes = repoRecord.DataBlock.ToBytes();
        Logger.LogInfo($"Hex:   {BitConverter.ToString(recordBytes).Replace("-", "")}");
    }

}
