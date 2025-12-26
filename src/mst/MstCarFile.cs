using System.Text;
using dnproto.sdk.repo;
using dnproto.sdk.log;

namespace dnproto.sdk.mst;

/// <summary>
/// Manages reading and writing MST repositories in CAR (Content Addressable aRchive) format.
/// 
/// CAR files are the standard serialization format for AT Protocol repositories.
/// They contain:
/// - Header with root CID(s)
/// - Series of blocks (CID + data), including:
///   - Commit object
///   - MST nodes
///   - Record data
/// </summary>
public class MstCarFile
{
    /// <summary>
    /// Write an MST repository to a CAR file.
    /// 
    /// The CAR file will contain:
    /// - Header pointing to the commit CID
    /// - Commit block
    /// - All MST node blocks
    /// - All record blocks
    /// </summary>
    public static void WriteToFile(string filePath, RepoCommit commit, MerkleSearchTree mst)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        WriteToStream(fs, commit, mst);
    }

    /// <summary>
    /// Write an MST repository to a stream in CAR format.
    /// </summary>
    public static void WriteToStream(Stream stream, RepoCommit commit, MerkleSearchTree mst)
    {
        // Ensure commit CID is computed
        var commitCid = commit.CommitCid ?? commit.ComputeCid();

        // Write CAR header
        WriteCarHeader(stream, commitCid);

        // Write commit block
        WriteBlock(stream, commitCid, commit.ToSignedDagCbor());

        // Write MST node blocks
        var nodes = mst.GetAllNodes();
        foreach (var kvp in nodes)
        {
            var nodeCid = kvp.Value.Cid;
            if (nodeCid != null)
            {
                WriteBlock(stream, nodeCid, kvp.Value.ToDagCbor());
            }
        }

        // Write record blocks
        var records = mst.GetAllRecords();
        foreach (var kvp in records)
        {
            // Parse CID from base32 string
            var cidBytes = Base32Encoding.Base32ToBytes(kvp.Key.Substring(1)); // Remove 'b' prefix
            using var cidMs = new MemoryStream(cidBytes);
            var cid = CidV1.ReadCid(cidMs);
            
            WriteBlock(stream, cid, kvp.Value);
        }
    }

    /// <summary>
    /// Write a CAR file with only specific blocks (for diffs).
    /// Used when generating firehose events to only include changed data.
    /// </summary>
    public static void WriteToStreamWithBlocks(Stream stream, RepoCommit commit, Dictionary<string, byte[]> blocks)
    {
        // Ensure commit CID is computed
        var commitCid = commit.CommitCid ?? commit.ComputeCid();

        // Write CAR header
        WriteCarHeader(stream, commitCid);

        // Write commit block
        WriteBlock(stream, commitCid, commit.ToSignedDagCbor());

        // Write only the specified blocks
        foreach (var kvp in blocks)
        {
            // Parse CID from base32 string
            var cidBytes = Base32Encoding.Base32ToBytes(kvp.Key.Substring(1)); // Remove 'b' prefix
            using var cidMs = new MemoryStream(cidBytes);
            var cid = CidV1.ReadCid(cidMs);
            
            WriteBlock(stream, cid, kvp.Value);
        }
    }

    /// <summary>
    /// Write the CAR header.
    /// Format: VarInt(header_length) + DAG-CBOR(header_object)
    /// Header object: { "roots": [commit_cid], "version": 1 }
    /// </summary>
    private static void WriteCarHeader(Stream stream, CidV1 rootCid)
    {
        using var headerMs = new MemoryStream();
        
        // Create header object
        var headerDict = new Dictionary<string, DagCborObject>();

        // "roots" array with single commit CID
        var rootsArray = new List<DagCborObject>
        {
            new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = rootCid
            }
        };

        headerDict["roots"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_ARRAY, AdditionalInfo = 0, OriginalByte = 0 },
            Value = rootsArray
        };

        // "version" = 1
        headerDict["version"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = 1
        };

        var headerObj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = headerDict
        };

        // Serialize header
        DagCborObject.WriteToStream(headerObj, headerMs);
        var headerBytes = headerMs.ToArray();

        // Write header length and bytes
        VarInt.WriteVarInt(stream, new VarInt { Value = headerBytes.Length });
        stream.Write(headerBytes, 0, headerBytes.Length);
    }

    /// <summary>
    /// Write a single block to the CAR file.
    /// Format: VarInt(block_length) + CID + data
    /// </summary>
    private static void WriteBlock(Stream stream, CidV1 cid, byte[] data)
    {
        using var blockMs = new MemoryStream();
        
        // Write CID
        CidV1.WriteCid(blockMs, cid);
        
        // Write data
        blockMs.Write(data, 0, data.Length);
        
        var blockBytes = blockMs.ToArray();

        // Write block length and bytes
        VarInt.WriteVarInt(stream, new VarInt { Value = blockBytes.Length });
        stream.Write(blockBytes, 0, blockBytes.Length);
    }

    /// <summary>
    /// Read an MST repository from a CAR file.
    /// Returns the commit and populated MST.
    /// </summary>
    public static (RepoCommit commit, MerkleSearchTree mst) ReadFromFile(string filePath, IDnProtoLogger? logger = null)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return ReadFromStream(fs, logger);
    }

    /// <summary>
    /// Read an MST repository from a stream in CAR format.
    /// </summary>
    public static (RepoCommit commit, MerkleSearchTree mst) ReadFromStream(Stream stream, IDnProtoLogger? logger = null)
    {
        // Read CAR header
        var (headerLength, header) = ReadCarHeader(stream);
        
        // Extract root CID
        CidV1? rootCid = null;
        if (header.Value is Dictionary<string, DagCborObject> headerDict &&
            headerDict.TryGetValue("roots", out var rootsObj) &&
            rootsObj.Value is List<DagCborObject> rootsList &&
            rootsList.Count > 0 &&
            rootsList[0].Value is CidV1 root)
        {
            rootCid = root;
        }

        if (rootCid == null)
        {
            throw new Exception("No root CID found in CAR header");
        }

        // Read all blocks
        var blocks = new Dictionary<string, byte[]>();
        
        while (stream.Position < stream.Length)
        {
            var (blockCid, blockData) = ReadBlock(stream);
            blocks[blockCid.Base32] = blockData;
        }

        // Parse commit (should be the root block)
        if (!blocks.TryGetValue(rootCid.Base32, out var commitData))
        {
            throw new Exception("Commit block not found in CAR file");
        }

        var commit = RepoCommit.FromDagCbor(commitData);
        commit.CommitCid = rootCid;

        // Build MST
        var mst = new MerkleSearchTree();

        // Load all blocks into MST
        int nodeCount = 0;
        int recordCount = 0;
        
        foreach (var kvp in blocks)
        {
            if (kvp.Key == rootCid.Base32)
            {
                continue; // Skip commit
            }

            // Parse as DagCborObject to inspect structure
            using var ms = new MemoryStream(kvp.Value);
            var obj = DagCborObject.ReadFromStream(ms);
            
            // Check if it's an MST node by looking for the "e" (entries) field
            bool isMstNode = obj.Value is Dictionary<string, DagCborObject> dict && dict.ContainsKey("e");
            
            // Reconstruct CID from key
            var cidBytes = Base32Encoding.Base32ToBytes(kvp.Key.Substring(1));
            using var cidMs = new MemoryStream(cidBytes);
            var cid = CidV1.ReadCid(cidMs);
            
            if (isMstNode)
            {
                // Parse as MST node
                var node = MstNode.FromDagCborObject(obj);
                mst.LoadNode(cid, node);
                nodeCount++;
            }
            else
            {
                // It's a record
                mst.LoadRecord(cid, kvp.Value);
                recordCount++;
            }
        }

        logger?.LogTrace($"Loaded {nodeCount} nodes and {recordCount} records from CAR file");

        // Set MST root from commit
        if (commit.DataCid != null)
        {
            var rootNode = mst.GetNodeByCid(commit.DataCid.Base32);
            
            if (rootNode != null)
            {
                mst.SetRoot(rootNode);
            }
        }

        return (commit, mst);
    }

    /// <summary>
    /// Read the CAR header.
    /// </summary>
    private static (VarInt length, DagCborObject header) ReadCarHeader(Stream stream)
    {
        var length = VarInt.ReadVarInt(stream);
        var header = DagCborObject.ReadFromStream(stream);
        return (length, header);
    }

    /// <summary>
    /// Read a single block from the CAR file.
    /// </summary>
    private static (CidV1 cid, byte[] data) ReadBlock(Stream stream)
    {
        // Read block length
        var blockLength = VarInt.ReadVarInt(stream);
        
        // Read block into buffer
        var blockBytes = new byte[blockLength.Value];
        int bytesRead = stream.Read(blockBytes, 0, (int)blockLength.Value);
        
        if (bytesRead != blockLength.Value)
        {
            throw new Exception($"Failed to read {blockLength.Value} bytes from stream. Read {bytesRead} bytes.");
        }

        // Parse CID and data
        using var blockMs = new MemoryStream(blockBytes);
        var cid = CidV1.ReadCid(blockMs);
        
        var dataLength = blockBytes.Length - (int)blockMs.Position;
        var data = new byte[dataLength];
        blockMs.Read(data, 0, dataLength);

        return (cid, data);
    }
}
