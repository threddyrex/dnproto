# Author Feed Functions

This document explains how to use the `AuthorFeed` class to retrieve posts and reposts from an MST repository.

## Overview

The `AuthorFeed` class provides functions to query an author's feed - posts and reposts they've created. These functions are useful for:
- Building user profile feeds
- Displaying recent activity
- Filtering content by type

## Basic Usage

### Get Author Feed (Posts + Reposts)

The main function `GetAuthorFeed` returns the 50 most recent posts and reposts:

```csharp
using dnproto.sdk.mst;

// Assuming you have an MstRepository instance
var repo = new MstRepository("did:plc:abc123");
// ... (add some records)

// Get the 50 most recent posts and reposts
var feed = AuthorFeed.GetAuthorFeed(repo);

// Display the feed
foreach (var item in feed)
{
    Console.WriteLine(item.ToString());
}
```

### Custom Limit

You can specify a different limit:

```csharp
// Get only the 10 most recent items
var recentItems = AuthorFeed.GetAuthorFeed(repo, limit: 10);

// Get 100 items
var moreItems = AuthorFeed.GetAuthorFeed(repo, limit: 100);
```

### Get Posts Only

To exclude reposts and get only original posts:

```csharp
var posts = AuthorFeed.GetAuthorPosts(repo, limit: 50);

foreach (var post in posts)
{
    Console.WriteLine($"Post: {post.Text}");
    Console.WriteLine($"Created: {post.CreatedAt}");
}
```

### Get Records from Specific Collection

To query any collection:

```csharp
// Get likes
var likes = AuthorFeed.GetRecordsByCollection(repo, "app.bsky.feed.like", limit: 50);

// Get follows
var follows = AuthorFeed.GetRecordsByCollection(repo, "app.bsky.graph.follow", limit: 50);

// Get blocks
var blocks = AuthorFeed.GetRecordsByCollection(repo, "app.bsky.graph.block", limit: 50);
```

## FeedItem Properties

Each `FeedItem` contains:

- `Path` - Full repository path (e.g., "app.bsky.feed.post/3kj1abc")
- `Collection` - Collection name (e.g., "app.bsky.feed.post")
- `Tid` - The TID (timestamp identifier)
- `RecordData` - Raw DAG-CBOR encoded bytes
- `RecordType` - The $type field from the record
- `CreatedAt` - ISO 8601 timestamp
- `Text` - For posts: the text content
- `SubjectUri` - For reposts: the URI being reposted

## Complete Example

```csharp
using System;
using System.Collections.Generic;
using dnproto.sdk.mst;
using dnproto.sdk.repo;

// Create a repository
var repo = new MstRepository("did:plc:example123");

// Create some posts
for (int i = 0; i < 10; i++)
{
    var postDict = new Dictionary<string, DagCborObject>
    {
        ["$type"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = "app.bsky.feed.post"
        },
        ["text"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = $"This is post number {i}"
        },
        ["createdAt"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        }
    };

    var postObj = new DagCborObject
    {
        Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
        Value = postDict
    };

    using var ms = new MemoryStream();
    DagCborObject.WriteToStream(postObj, ms);
    var recordData = ms.ToArray();

    var tid = RecordKey.GenerateTid();
    repo.CreateRecord($"app.bsky.feed.post/{tid}", recordData);
}

// Commit the changes
repo.Commit(hash => hash); // Use a real signing function in production

// Get the author feed
var feed = AuthorFeed.GetAuthorFeed(repo, limit: 5);

Console.WriteLine($"Found {feed.Count} feed items:");
foreach (var item in feed)
{
    Console.WriteLine($"  {item}");
}
```

## How It Works

### TID-Based Sorting

TIDs (Timestamp Identifiers) are 13-character base32-sortable strings that encode:
- 53 bits: microseconds since UNIX epoch
- 10 bits: random clock identifier

Because TIDs are sortable, we can sort records by their TID to get chronological order:

```csharp
// Extract TID from path like "app.bsky.feed.post/3kj1abc123"
string tid = path.Substring(path.LastIndexOf('/') + 1);

// Sort in descending order (newest first)
keys.Sort((a, b) => string.Compare(tidB, tidA));
```

### Collection Filtering

Records are organized by collection:
- `app.bsky.feed.post/` - Posts
- `app.bsky.feed.repost/` - Reposts
- `app.bsky.feed.like/` - Likes
- `app.bsky.graph.follow/` - Follows

We filter by checking if the path starts with the collection name:

```csharp
var posts = allKeys.Where(key => key.StartsWith("app.bsky.feed.post/"));
```

## Performance Considerations

- **In-Memory Scanning**: The current implementation scans all records in memory. For large repositories, consider adding indexes.
- **Caching**: Consider caching feed results if queried frequently.
- **Pagination**: For very large feeds, implement pagination using cursor-based pagination with TIDs.

## Future Enhancements

Possible improvements:
1. **Time-Range Queries**: Filter by date range using TID timestamps
2. **Cursor Pagination**: Use `before` and `after` cursors for efficient pagination
3. **Composite Feeds**: Merge feeds from multiple repositories
4. **Filtering**: Add text search, tag filtering, etc.
5. **Indexing**: Build reverse indexes for faster queries

## Related Functions

- `RecordKey.GenerateTid()` - Generate a new TID
- `RecordKey.GetTidTimestamp(tid)` - Extract timestamp from TID
- `MstRepository.ListRecords()` - List all record paths
- `MstRepository.GetRecord(path)` - Get record data by path
