# MST Diff Implementation

## Overview

The MST library now implements **proper diff calculation** for firehose events. Only blocks that have actually changed are included in the `blocks` field of commit events.

## How It Works

### 1. Track Changed Records

During `Commit()`, we track which records were modified:

```csharp
var changedRecordCids = new HashSet<string>();

// When adding/updating records:
var cid = Mst.Put(path, data);
changedRecordCids.Add(cid.Base32);
```

### 2. Compare MST Trees

The `GetChangedBlocks()` method compares the old and new MST roots:

```csharp
public Dictionary<string, byte[]> GetChangedBlocks(
    CidV1? oldRootCid, 
    CidV1? newRootCid, 
    HashSet<string> changedRecordCids)
```

**Algorithm:**

1. Start at both root nodes (old and new)
2. Compare CIDs:
   - If CIDs match → subtree unchanged, skip traversal
   - If CIDs differ → node has changed, add to result
3. Recursively traverse changed subtrees:
   - Compare left children
   - Compare entry subtrees
   - Only descend into branches that changed

### 3. Tree Traversal Logic

```
Old Root (CID: abc)          New Root (CID: xyz)
    |                             |
    +--> CIDs differ? YES         |
         Add new root             |
         |                        |
         Compare children ------->|
              |                   |
         Left child               |
         CIDs differ? NO -------> Skip (unchanged)
              |
         Entry subtrees
         CIDs differ? YES ------> Add changed nodes
```

### 4. Collect New Subtrees

If an entire subtree is new (old node is null), we collect all nodes in that subtree:

```csharp
private void CollectAllNodesInSubtree(MstNode node, Dictionary<string, byte[]> blocks)
```

This ensures we include all necessary data for new branches.

## What Gets Included

For a single record creation/update:

### Included Blocks:
- ✅ **1 commit block** - The new commit object
- ✅ **1 record block** - The new/updated record
- ✅ **1-5 MST node blocks** - Only nodes on the path from root to the changed entry
  - Root node (always changes when tree changes)
  - Intermediate nodes (depth-based)
  - Leaf node containing the entry

### Excluded Blocks:
- ❌ Unchanged MST nodes (most of the tree)
- ❌ Unchanged records
- ❌ Deleted record data (only path in `ops`, not the bytes)

## Performance Impact

### Before (Full Repo):
```
Repository with 1000 posts, add 1 new post:
- Blocks sent: ~1002 (1000 records + MST nodes + commit)
- Typical size: 500KB - 2MB
```

### After (Proper Diff):
```
Repository with 1000 posts, add 1 new post:
- Blocks sent: ~5 (1 commit + 3 MST nodes + 1 record)
- Typical size: 2-10KB
```

**Improvement: 100x smaller for typical updates!**

## Example

### Adding One Post to Existing Repo

```csharp
var repo = MstRepository.LoadFromFile("repo.car");

// Add one post
var tid = RecordKey.GenerateTid();
repo.CreateRecord($"app.bsky.feed.post/{tid}", postBytes);

// Commit generates diff
var event = repo.Commit(signer);

// event.Blocks contains only:
// 1. Commit block
// 2. Root MST node (changed because tree changed)
// 3. Intermediate MST node (if key depth > 0)
// 4. Leaf MST node (containing the new entry)
// 5. Record block (the new post)
```

### Deleting One Post

```csharp
repo.DeleteRecord("app.bsky.feed.post/3kj1old");
var event = repo.Commit(signer);

// event.Blocks contains:
// 1. Commit block
// 2-4. Changed MST nodes on path to deleted entry
// (No record block - deletion doesn't include old data)
```

### Batch Update (3 Posts)

```csharp
repo.CreateRecord($"app.bsky.feed.post/{tid1}", post1);
repo.CreateRecord($"app.bsky.feed.post/{tid2}", post2);
repo.UpdateRecord($"app.bsky.feed.post/{tid3}", updatedPost);

var event = repo.Commit(signer);

// event.Blocks contains:
// 1. Commit block
// 2-8. MST nodes on paths to all 3 changed entries (some may overlap)
// 9-11. The 3 record blocks
```

## Implementation Details

### Key Methods

**MerkleSearchTree.cs:**
- `GetChangedBlocks()` - Main entry point for diff calculation
- `CollectChangedNodes()` - Recursive tree comparison
- `CollectAllNodesInSubtree()` - Collect all nodes in new subtrees

**MstRepository.cs:**
- `Commit()` - Tracks changed record CIDs
- `GenerateDiffBlocks()` - Generates CAR bytes with only changed blocks

**MstCarFile.cs:**
- `WriteToStreamWithBlocks()` - Writes CAR with specific block set

### Tree Comparison Strategy

The algorithm uses **CID-based comparison** at each node:

1. **Fast path**: If CIDs match, entire subtree is identical → skip
2. **Slow path**: If CIDs differ, node changed → recurse to find what changed

This is efficient because:
- Most of the tree is unchanged in typical commits
- CID comparison is O(1)
- We only traverse changed branches

### Edge Cases Handled

1. **First commit** (no old tree):
   - Old root is null
   - All nodes are "changed"
   - Collects entire tree

2. **Empty tree** (no records):
   - Returns empty blocks
   - Only commit block

3. **Overlapping paths**:
   - Multiple changes might share parent nodes
   - Dictionary ensures no duplicates

## Verification

To verify the diff is working correctly:

```csharp
var repo = MstRepository.LoadFromFile("repo.car");
var initialBlockCount = repo.Mst.GetAllNodes().Count;

// Add one record
repo.CreateRecord($"app.bsky.feed.post/{tid}", postBytes);
var event = repo.Commit(signer);

// Parse blocks from event
var blocksMs = new MemoryStream(event.Blocks);
var (header, _) = MstCarFile.ReadFromStream(blocksMs);

// Count blocks (excluding header)
int blockCount = 0;
while (blocksMs.Position < blocksMs.Length)
{
    blockCount++;
    var (_, _) = MstCarFile.ReadBlock(blocksMs);
}

Console.WriteLine($"Total nodes in tree: {initialBlockCount}");
Console.WriteLine($"Blocks in diff: {blockCount}");
// Should see: Total=100s-1000s, Diff=5-10
```

## Benefits

1. **Bandwidth Efficiency**: 100x smaller firehose events
2. **Network Performance**: Faster propagation across federation
3. **Storage Efficiency**: Smaller event logs
4. **Compliance**: Matches AT Protocol specification
5. **Scalability**: Works efficiently even with large repositories

## Limitations

The current diff implementation doesn't optimize for:
- **Block deduplication across multiple commits** - Each diff is independent
- **Compression** - CAR format is uncompressed
- **Streaming** - Entire diff is generated in memory

These could be future optimizations if needed.
