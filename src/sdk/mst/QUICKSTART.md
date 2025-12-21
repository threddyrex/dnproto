# MST Library - Quick Start Guide

## Installation

The MST library is located in `src/sdk/mst/` and is part of the dnproto project. No additional installation needed.

## Basic Usage

### 1. Create a New Repository

```csharp
using dnproto.sdk.mst;
using dnproto.sdk.repo;

// Define signing function (use your crypto library)
Func<byte[], byte[]> signingFunction = (hash) => {
    // Sign the hash with your private key
    // Example using placeholder:
    return hash; // Replace with actual signature
};

// Create repository for a new user
string did = "did:plc:abc123xyz";
var repo = MstRepository.CreateForNewUser(did, signingFunction);
```

### 2. Add Records

```csharp
// Create a record (DAG-CBOR encoded)
// Example: A simple post
using var ms = new MemoryStream();
var postDict = new Dictionary<string, DagCborObject>();

postDict["$type"] = new DagCborObject
{
    Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
    Value = "app.bsky.feed.post"
};

postDict["text"] = new DagCborObject
{
    Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
    Value = "Hello, AT Protocol!"
};

postDict["createdAt"] = new DagCborObject
{
    Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
    Value = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
};

var postObj = new DagCborObject
{
    Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
    Value = postDict
};

DagCborObject.WriteToStream(postObj, ms);
var recordBytes = ms.ToArray();

// Generate TID for record key
var tid = RecordKey.GenerateTid();
string path = $"app.bsky.feed.post/{tid}";

// Add to repository (pending until commit)
repo.CreateRecord(path, recordBytes);
```

### 3. Commit Changes

```csharp
// Commit all pending changes
var firehoseEvent = repo.Commit(signingFunction);

// The firehose event contains:
Console.WriteLine($"Revision: {firehoseEvent.Rev}");
Console.WriteLine($"Sequence: {firehoseEvent.Seq}");
Console.WriteLine($"Operations: {firehoseEvent.Ops.Count}");

foreach (var op in firehoseEvent.Ops)
{
    Console.WriteLine($"  {op.Action}: {op.Path}");
}
```

### 4. Save to Disk

```csharp
// Save repository to CAR file
string fileName = $"repos/{did.Replace(":", "_")}.car";
repo.SaveToFile(fileName);
```

### 5. Load from Disk

```csharp
// Load existing repository
var loadedRepo = MstRepository.LoadFromFile(fileName);

// List all records
var keys = loadedRepo.ListRecords();
foreach (var key in keys)
{
    Console.WriteLine(key);
}
```

### 6. Update and Delete Records

```csharp
// Update existing record
repo.UpdateRecord(path, updatedRecordBytes);

// Delete record
repo.DeleteRecord(path);

// Commit changes
var updateEvent = repo.Commit(signingFunction);
```

### 7. Emit Firehose Events

```csharp
// After each commit, emit firehose event
var firehoseEvent = repo.Commit(signingFunction);

// Serialize to WebSocket wire format
byte[] wireBytes = firehoseEvent.ToFirehoseBytes();

// Send to subscribers over WebSocket
// websocket.Send(wireBytes);
```

## Complete Example

```csharp
using dnproto.sdk.mst;
using dnproto.sdk.repo;

public class MstQuickStart
{
    public static void Run()
    {
        // 1. Setup
        Func<byte[], byte[]> signer = (hash) => hash; // Replace with real signing
        string did = "did:plc:example123";
        
        // 2. Create repository
        var repo = MstRepository.CreateForNewUser(did, signer);
        
        // 3. Add multiple records
        for (int i = 0; i < 3; i++)
        {
            var tid = RecordKey.GenerateTid();
            var recordData = CreatePost($"Post #{i}");
            repo.CreateRecord($"app.bsky.feed.post/{tid}", recordData);
        }
        
        // 4. Commit
        var event = repo.Commit(signer);
        Console.WriteLine($"Created {event.Ops.Count} records");
        
        // 5. Save
        repo.SaveToFile("example.car");
        
        // 6. Load
        var loaded = MstRepository.LoadFromFile("example.car");
        Console.WriteLine($"Loaded {loaded.ListRecords().Count} records");
        
        // 7. Emit firehose
        var wireBytes = event.ToFirehoseBytes();
        Console.WriteLine($"Firehose event: {wireBytes.Length} bytes");
    }
    
    static byte[] CreatePost(string text)
    {
        using var ms = new MemoryStream();
        var dict = new Dictionary<string, DagCborObject>
        {
            ["$type"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = "app.bsky.feed.post"
            },
            ["text"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = text
            },
            ["createdAt"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            }
        };
        
        var obj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = dict
        };
        
        DagCborObject.WriteToStream(obj, ms);
        return ms.ToArray();
    }
}
```

## Key Concepts

### Repository Paths

Records are identified by paths in the format:
```
<collection>/<recordKey>
```

Examples:
- `app.bsky.feed.post/3kj1abc123`
- `app.bsky.feed.like/3kj2def456`
- `app.bsky.graph.follow/3kj3ghi789`

### TID Generation

Record keys should use TIDs (Timestamp Identifiers):

```csharp
var tid = RecordKey.GenerateTid();
// Returns: "3kj1abc123def" (13 characters, base32-sortable)
```

TIDs ensure chronological ordering within collections.

### Commit Flow

```
1. CreateRecord/UpdateRecord/DeleteRecord (pending changes)
   ↓
2. Commit (applies changes to MST)
   ↓
3. Generate firehose event
   ↓
4. SaveToFile (persist to disk)
   ↓
5. Emit firehose event (broadcast to subscribers)
```

### Firehose Event Structure

Each commit generates one firehose event containing:

- **Header**: `{ "t": "#commit", "op": 1 }`
- **Body**:
  - `ops`: Array of operations
  - `repo`: DID
  - `rev`: New revision TID
  - `seq`: Sequence number
  - `commit`: Commit CID
  - `blocks`: CAR bytes with changes
  - And more...

## Error Handling

```csharp
try
{
    var repo = MstRepository.LoadFromFile("repo.car");
    repo.CreateRecord(path, data);
    var event = repo.Commit(signer);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Performance Tips

1. **Batch changes**: Add multiple records before calling `Commit()`
2. **Sequence numbers**: Track sequence numbers for resume capability
3. **CAR files**: Store one CAR file per user/DID
4. **Cache management**: The MST keeps nodes/records in memory - clear caches for very large repos

## Next Steps

- Read [README.md](README.md) for detailed documentation
- Read [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) for architecture details
- See AT Protocol specs at https://atproto.com/specs/repository
