using System.Text;
using dnproto.sdk.repo;
using dnproto.sdk.log;

namespace dnproto.sdk.mst;

/// <summary>
/// Manages an AT Protocol repository with MST, commits, and firehose events.
/// 
/// This is the main entry point for working with repositories. It handles:
/// - Creating new repositories
/// - Adding/updating/deleting records
/// - Committing changes
/// - Generating firehose events
/// - Saving/loading from disk (CAR format)
/// 
/// Example usage:
/// <code>
/// var repo = new MstRepository("did:plc:abc123");
/// repo.CreateRecord("app.bsky.feed.post/3kj1abc", postData);
/// var events = repo.Commit(signingFunction);
/// repo.SaveToFile("repo.car");
/// </code>
/// </summary>
public class MstRepository
{
    /// <summary>
    /// The DID of the account that owns this repository.
    /// </summary>
    public string Did { get; private set; }

    /// <summary>
    /// The Merkle Search Tree containing all records.
    /// </summary>
    public MerkleSearchTree Mst { get; private set; }

    /// <summary>
    /// The current (most recent) commit.
    /// </summary>
    public RepoCommit? CurrentCommit { get; private set; }

    /// <summary>
    /// Sequence number for firehose events.
    /// Increments with each commit.
    /// </summary>
    public long SequenceNumber { get; set; }

    /// <summary>
    /// Track pending changes (before commit).
    /// Maps path -> (action, record_data)
    /// </summary>
    private Dictionary<string, (string action, byte[]? data)> _pendingChanges = new Dictionary<string, (string, byte[]?)>();

    /// <summary>
    /// Create a new repository for a DID.
    /// </summary>
    public MstRepository(string did)
    {
        Did = did;
        Mst = new MerkleSearchTree();
        SequenceNumber = 0;
    }

    /// <summary>
    /// Create a repository from an existing commit and MST.
    /// </summary>
    public MstRepository(string did, RepoCommit commit, MerkleSearchTree mst, long seq = 0)
    {
        Did = did;
        CurrentCommit = commit;
        Mst = mst;
        SequenceNumber = seq;
    }

    /// <summary>
    /// Create a new record in the repository.
    /// Changes are pending until Commit() is called.
    /// </summary>
    /// <param name="path">Repository path (e.g., "app.bsky.feed.post/3kj1...")</param>
    /// <param name="record">DAG-CBOR encoded record data</param>
    public void CreateRecord(string path, byte[] record)
    {
        _pendingChanges[path] = ("create", record);
    }

    /// <summary>
    /// Update an existing record.
    /// Changes are pending until Commit() is called.
    /// </summary>
    public void UpdateRecord(string path, byte[] record)
    {
        _pendingChanges[path] = ("update", record);
    }

    /// <summary>
    /// Delete a record.
    /// Changes are pending until Commit() is called.
    /// </summary>
    public void DeleteRecord(string path)
    {
        _pendingChanges[path] = ("delete", null);
    }

    /// <summary>
    /// Get a record by path.
    /// </summary>
    public byte[]? GetRecord(string path)
    {
        // Check pending changes first
        if (_pendingChanges.TryGetValue(path, out var change))
        {
            return change.data;
        }

        // Get from MST
        return Mst.Get(path);
    }

    /// <summary>
    /// List all record keys in the repository.
    /// </summary>
    public List<string> ListRecords()
    {
        return Mst.ListKeys();
    }

    /// <summary>
    /// Commit all pending changes to the repository.
    /// 
    /// This will:
    /// 1. Apply changes to the MST
    /// 2. Create a new commit object
    /// 3. Sign the commit
    /// 4. Generate firehose event
    /// 5. Clear pending changes
    /// 
    /// Returns the firehose event that should be emitted.
    /// </summary>
    /// <param name="signingFunction">Function to sign the commit hash</param>
    /// <returns>Firehose event to emit</returns>
    public FirehoseCommitEvent Commit(Func<byte[], byte[]> signingFunction)
    {
        if (_pendingChanges.Count == 0)
        {
            throw new Exception("No pending changes to commit");
        }

        // Track operations for firehose event and changed record CIDs
        var ops = new List<RepoOperation>();
        var changedRecordCids = new HashSet<string>();

        // Apply changes to MST
        foreach (var kvp in _pendingChanges)
        {
            var path = kvp.Key;
            var (action, data) = kvp.Value;

            if (action == "create" || action == "update")
            {
                if (data != null)
                {
                    var cid = Mst.Put(path, data);
                    changedRecordCids.Add(cid.Base32);
                    
                    ops.Add(new RepoOperation
                    {
                        Action = action,
                        Path = path,
                        Cid = cid
                    });
                }
            }
            else if (action == "delete")
            {
                Mst.Delete(path);
                
                ops.Add(new RepoOperation
                {
                    Action = action,
                    Path = path
                });
            }
        }

        // Get previous data CID
        var prevDataCid = CurrentCommit?.DataCid;

        // Create new commit
        var newRev = RecordKey.GenerateTid();
        var newCommit = new RepoCommit
        {
            Did = Did,
            Version = 3,
            Rev = newRev,
            PrevCid = null, // v3 repos typically have null prev
            DataCid = Mst.Root?.ComputeCid()
        };

        // Sign the commit
        newCommit.Sign(signingFunction);

        // Compute commit CID
        var commitCid = newCommit.ComputeCid();

        // Update current commit
        var previousRev = CurrentCommit?.Rev;
        CurrentCommit = newCommit;

        // Increment sequence
        SequenceNumber++;

        // Generate firehose event
        var firehoseEvent = new FirehoseCommitEvent
        {
            Ops = ops,
            Repo = Did,
            Rev = newRev,
            Since = previousRev,
            Seq = SequenceNumber,
            Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            Commit = commitCid,
            PrevData = prevDataCid,
            Blocks = GenerateDiffBlocks(prevDataCid, newCommit.DataCid, changedRecordCids),
            Blobs = new List<CidV1>(),
            Rebase = false,
            TooBig = false
        };

        // Clear pending changes
        _pendingChanges.Clear();

        return firehoseEvent;
    }

    /// <summary>
    /// Generate the diff blocks (CAR format) containing only changed data.
    /// 
    /// Compares the old and new MST trees and only includes:
    /// - The commit block
    /// - Changed MST node blocks (on the path to modified entries)
    /// - New/updated record blocks
    /// </summary>
    private byte[] GenerateDiffBlocks(CidV1? oldDataCid, CidV1? newDataCid, HashSet<string> changedRecordCids)
    {
        using var ms = new MemoryStream();
        
        if (newDataCid == null || CurrentCommit == null)
        {
            return Array.Empty<byte>();
        }

        // Get only changed blocks (nodes and records)
        var changedBlocks = Mst.GetChangedBlocks(oldDataCid, newDataCid, changedRecordCids);

        // Write as CAR format with only changed blocks
        MstCarFile.WriteToStreamWithBlocks(ms, CurrentCommit, changedBlocks);
        
        return ms.ToArray();
    }

    /// <summary>
    /// Save the repository to a CAR file.
    /// </summary>
    public void SaveToFile(string filePath)
    {
        if (CurrentCommit == null)
        {
            throw new Exception("No commit to save. Call Commit() first.");
        }

        MstCarFile.WriteToFile(filePath, CurrentCommit, Mst);
    }

    /// <summary>
    /// Load a repository from a CAR file.
    /// </summary>
    public static MstRepository LoadFromFile(string filePath, ILogger? logger = null)
    {
        var (commit, mst) = MstCarFile.ReadFromFile(filePath, logger);
        
        return new MstRepository(commit.Did, commit, mst);
    }

    /// <summary>
    /// Create a new empty repository for a user.
    /// This initializes with an empty MST and creates the first commit.
    /// </summary>
    public static MstRepository CreateForNewUser(string did, Func<byte[], byte[]> signingFunction)
    {
        var repo = new MstRepository(did);
        
        // Create initial commit with empty MST
        var initialRev = RecordKey.GenerateTid();
        var initialCommit = new RepoCommit
        {
            Did = did,
            Version = 3,
            Rev = initialRev,
            PrevCid = null,
            DataCid = repo.Mst.Root?.ComputeCid()
        };

        initialCommit.Sign(signingFunction);
        initialCommit.ComputeCid();

        repo.CurrentCommit = initialCommit;
        repo.SequenceNumber = 1;

        return repo;
    }

    /// <summary>
    /// Get statistics about the repository.
    /// </summary>
    public (int nodeCount, int recordCount) GetStats()
    {
        var nodes = Mst.GetAllNodes();
        var records = Mst.GetAllRecords();
        
        return (nodes.Count, records.Count);
    }

    /// <summary>
    /// Get the CAR file version used by this repository.
    /// AT Protocol uses CAR version 1.
    /// </summary>
    public int GetCarVersion()
    {
        return 1;
    }

    /// <summary>
    /// Get the CAR file roots (CIDs that serve as entry points).
    /// For AT Protocol repositories, this is typically just the commit CID.
    /// </summary>
    public CidV1[] GetRoots()
    {
        if (CurrentCommit?.CommitCid != null)
        {
            return new[] { CurrentCommit.CommitCid };
        }
        return Array.Empty<CidV1>();
    }
}
