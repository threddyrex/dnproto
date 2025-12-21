# MST (Merkle Search Tree) Library

This library implements the Merkle Search Tree (MST) data structure used in AT Protocol repositories.

## Overview

The MST is a content-addressed tree structure that stores key/value mappings in sorted order. It provides:

- **Content Addressing**: Deterministic CIDs based on content
- **Sorted Storage**: Keys are stored in lexicographic order
- **Efficient Updates**: Only changed nodes need to be rewritten
- **Cryptographic Verification**: Merkle tree properties for integrity

## Key Classes

### MstNode
Represents a single node in the MST tree.

Properties:
- `LeftCid` - Link to left subtree (keys sorting before all entries) - maps to **"l"** in log
- `Entries` - List of entries in this node - maps to **"e"** in log

Each entry has:
- `PrefixLength` - Bytes shared with previous entry - maps to **"p"** in log
- `KeySuffix` - Remainder of key after prefix - maps to **"k"** in log (base64 in JSON)
- `ValueCid` - CID link to record data - maps to **"v"** in log
- `TreeCid` - Optional CID link to right subtree - maps to **"t"** in log

### MerkleSearchTree
The main MST implementation with operations:

```csharp
// Create new tree
var mst = new MerkleSearchTree();

// Add/update records
var cid = mst.Put("app.bsky.feed.post/3kj1abc", recordBytes);

// Get records
var record = mst.Get("app.bsky.feed.post/3kj1abc");

// Delete records
mst.Delete("app.bsky.feed.post/3kj1abc");

// List all keys
var keys = mst.ListKeys();
```

### RepoCommit
Represents a signed commit object that points to an MST root.

Properties match the commit spec:
- `Did` - Account DID
- `Version` - Repo format version (3)
- `DataCid` - CID link to MST root
- `Rev` - Revision TID
- `PrevCid` - Previous commit CID (usually null in v3)
- `Signature` - Cryptographic signature

### MstRepository
High-level repository management.

```csharp
// Create new repository
var repo = new MstRepository("did:plc:abc123");

// Add records (pending until commit)
repo.CreateRecord("app.bsky.feed.post/3kj1abc", postBytes);
repo.UpdateRecord("app.bsky.feed.post/3kj1def", updatedBytes);
repo.DeleteRecord("app.bsky.feed.post/3kj1xyz");

// Commit changes and get firehose event
var firehoseEvent = repo.Commit(signingFunction);

// Save to disk
repo.SaveToFile("repo.car");

// Load from disk
var loadedRepo = MstRepository.LoadFromFile("repo.car");
```

### FirehoseCommitEvent
Represents a firehose event emitted when a repository changes.

Properties from firehose log:
- `Ops` - List of operations (create/update/delete)
- `Repo` - Repository DID
- `Rev` - New revision
- `Since` - Previous revision
- `Seq` - Sequence number
- `Time` - Timestamp
- `Commit` - Commit CID
- `PrevData` - Previous MST root CID
- `Blocks` - CAR bytes with diff data
- `Blobs` - Referenced blob CIDs
- `Rebase` - Is rebase operation
- `TooBig` - Commit too large flag

## Firehose Events

When a commit happens, the library generates firehose events that can be broadcast:

```csharp
var repo = new MstRepository("did:plc:abc123");
repo.CreateRecord("app.bsky.feed.post/3kj1abc", postBytes);

var event = repo.Commit(signingFunction);

// Serialize to firehose format (header + body)
var firehoseBytes = event.ToFirehoseBytes();

// Send over WebSocket...
```

### How Many Entries Change?

When a single record is added/updated/deleted:

1. **1 record block** changes (the record itself)
2. **1-N MST node blocks** change (nodes on the path from root to the affected entry)
   - At minimum: 1 node (if tree has only depth 0)
   - Typically: 2-5 nodes (depending on key depth and tree structure)
3. **1 commit block** changes (new commit object)

Example from log: A single post creation resulted in:
- 1 new record block
- Multiple MST node blocks (the log shows several nodes with "e" arrays)
- 1 commit block

## CAR File Format

Repositories are stored in CAR (Content Addressable aRchive) format:

```
[VarInt: header_length][DAG-CBOR: header]
[VarInt: block1_length][CID: block1_cid][bytes: block1_data]
[VarInt: block2_length][CID: block2_cid][bytes: block2_data]
...
```

The `MstCarFile` class handles reading and writing CAR files:

```csharp
// Write
MstCarFile.WriteToFile("repo.car", commit, mst);

// Read
var (commit, mst) = MstCarFile.ReadFromFile("repo.car");
```

## Key Depth Calculation

Keys are assigned a depth based on SHA-256 hash:

```csharp
int depth = MstNode.GetKeyDepth("app.bsky.feed.post/3kj1abc");
```

- Hash the key with SHA-256
- Count leading zeros in 2-bit chunks
- This gives fanout of 4

Examples from spec:
- "2653ae71" → depth 0
- "blue" → depth 1  
- "app.bsky.feed.post/454397e440ec" → depth 4
- "app.bsky.feed.post/9adeb165882c" → depth 8

## Dependencies

The MST library uses existing dnproto components:

- **CidV1** - CID encoding/decoding
- **DagCborObject** - DAG-CBOR serialization
- **VarInt** - Variable integer encoding
- **RecordKey** - TID generation
- **Base32Encoding** - Base32 encoding for CIDs

## Specifications

- [Repository Spec](https://atproto.com/specs/repository)
- [Record Key Spec](https://atproto.com/specs/record-key)
- [Data Model Spec](https://atproto.com/specs/data-model)
- [Event Stream Spec](https://atproto.com/specs/event-stream)
- [Sync Spec](https://atproto.com/specs/sync)

## Example: Creating a New User Repository

```csharp
using dnproto.sdk.mst;
using dnproto.sdk.repo;

// Signing function (use your crypto library)
Func<byte[], byte[]> signingFunc = (hash) => {
    // Sign hash with private key
    return signature;
};

// Create new repository for user
var repo = MstRepository.CreateForNewUser("did:plc:abc123", signingFunc);

// Add first post
var postData = /* DAG-CBOR encoded post */;
repo.CreateRecord("app.bsky.feed.post/3kj1abc", postData);

// Commit and get firehose event
var event = repo.Commit(signingFunc);

// Save repository
repo.SaveToFile("repos/did_plc_abc123.car");

// Emit firehose event
var firehoseBytes = event.ToFirehoseBytes();
// ... broadcast to subscribers
```

## Example: Loading and Updating Repository

```csharp
// Load existing repository
var repo = MstRepository.LoadFromFile("repos/did_plc_abc123.car");

// Make changes
repo.CreateRecord("app.bsky.feed.post/3kj1def", newPostData);
repo.DeleteRecord("app.bsky.feed.post/3kj1old");

// Commit changes
var event = repo.Commit(signingFunc);

// Save updated repository
repo.SaveToFile("repos/did_plc_abc123.car");

// Stats
var (nodeCount, recordCount) = repo.GetStats();
Console.WriteLine($"Nodes: {nodeCount}, Records: {recordCount}");
```

## Thread Safety

The current implementation is **not thread-safe**. If you need concurrent access:

1. Use external locking when modifying a repository
2. Create separate repository instances per thread
3. Implement a repository manager with locking

## Performance Considerations

- **Caching**: The MST keeps nodes and records in memory caches for fast access
- **Incremental Updates**: Only changed nodes are rewritten on commit
- **Prefix Compression**: Keys are compressed within nodes to save space
- **Depth-based Organization**: Keys are organized by hash depth for balanced tree

## Limitations

1. **In-Memory Only**: All nodes and records are kept in memory. For large repositories, consider implementing a persistent block store.

2. **No Validation**: The library doesn't validate:
   - Maximum entries per node
   - Maximum tree depth
   - Record schema validation
   
3. **Simplified CAR Reading**: The CAR reader doesn't fully reconstruct the MST tree structure from CIDs.

## Future Enhancements

- Implement persistent block storage
- Add support for blob references
- Add validation for MST structure constraints (max entries, max depth)
- Optimize prefix compression algorithm
- Add MST tree balancing checks
