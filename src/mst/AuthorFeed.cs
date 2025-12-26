using System.Text;
using dnproto.sdk.repo;

namespace dnproto.sdk.mst;

/// <summary>
/// Provides feed-related operations for an MST repository.
/// 
/// This includes functions like getAuthorFeed which retrieve and filter
/// records from a repository based on specific criteria.
/// </summary>
public static class AuthorFeed
{
    /// <summary>
    /// Get the 50 most recent posts and reposts from a repository.
    /// 
    /// This function:
    /// 1. Scans all records in the repository
    /// 2. Filters for records in "app.bsky.feed.post" and "app.bsky.feed.repost" collections
    /// 3. Sorts by the record key (TID) in descending order (newest first)
    /// 4. Returns the top 50 records
    /// 
    /// Note: TIDs are timestamp-based identifiers, so sorting by TID gives
    /// chronological order.
    /// </summary>
    /// <param name="repo">The repository to query</param>
    /// <returns>List of (path, recordData) tuples for the most recent feed items</returns>
    public static List<FeedItem> GetAuthorFeed(MstRepository repo, int limit = 50)
    {
        var feedItems = new List<FeedItem>();
        
        // Get all record paths
        var allKeys = repo.ListRecords();
        
        // Filter for post and repost records
        var feedKeys = allKeys.Where(key => 
            key.StartsWith("app.bsky.feed.post/") || 
            key.StartsWith("app.bsky.feed.repost/")
        ).ToList();
        
        // Sort by TID (record key) in descending order (newest first)
        // Since TIDs are timestamp-based, we can sort by the TID portion of the path
        feedKeys.Sort((a, b) => 
        {
            // Extract TID from path (everything after the last '/')
            string tidA = a.Substring(a.LastIndexOf('/') + 1);
            string tidB = b.Substring(b.LastIndexOf('/') + 1);
            
            // Compare in reverse order (newest first)
            return string.Compare(tidB, tidA, StringComparison.Ordinal);
        });
        
        // Take the top N (default 50)
        var topKeys = feedKeys.Take(limit);
        
        // Retrieve the record data for each key
        foreach (var key in topKeys)
        {
            var recordData = repo.GetRecord(key);
            if (recordData != null)
            {
                // Parse the record to extract metadata
                var item = ParseFeedItem(key, recordData);
                feedItems.Add(item);
            }
        }
        
        return feedItems;
    }

    /// <summary>
    /// Parse a record into a FeedItem with extracted metadata.
    /// </summary>
    private static FeedItem ParseFeedItem(string path, byte[] recordData)
    {
        var item = new FeedItem
        {
            Path = path,
            RecordData = recordData
        };

        try
        {
            // Parse the DAG-CBOR record
            using var ms = new MemoryStream(recordData);
            var dagObj = DagCborObject.ReadFromStream(ms);
            
            // Extract $type and createdAt if they exist
            if (dagObj.Value is Dictionary<string, DagCborObject> dict)
            {
                if (dict.TryGetValue("$type", out var typeObj) && typeObj.Value is string type)
                {
                    item.RecordType = type;
                }
                
                if (dict.TryGetValue("createdAt", out var createdAtObj) && createdAtObj.Value is string createdAt)
                {
                    item.CreatedAt = createdAt;
                }
                
                // For posts, extract text
                if (dict.TryGetValue("text", out var textObj) && textObj.Value is string text)
                {
                    item.Text = text;
                }
                
                // For reposts, extract subject
                if (dict.TryGetValue("subject", out var subjectObj) && subjectObj.Value is Dictionary<string, DagCborObject> subjectDict)
                {
                    if (subjectDict.TryGetValue("uri", out var uriObj) && uriObj.Value is string uri)
                    {
                        item.SubjectUri = uri;
                    }
                }
            }
        }
        catch
        {
            // If parsing fails, just return the basic item with raw data
        }

        // Extract collection and TID from path
        var parts = path.Split('/');
        if (parts.Length == 2)
        {
            item.Collection = parts[0];
            item.Tid = parts[1];
        }

        return item;
    }

    /// <summary>
    /// Get posts only (excluding reposts).
    /// </summary>
    public static List<FeedItem> GetAuthorPosts(MstRepository repo, int limit = 50)
    {
        var allKeys = repo.ListRecords();
        
        var postKeys = allKeys
            .Where(key => key.StartsWith("app.bsky.feed.post/"))
            .ToList();
        
        // Sort by TID descending (newest first)
        postKeys.Sort((a, b) => 
        {
            string tidA = a.Substring(a.LastIndexOf('/') + 1);
            string tidB = b.Substring(b.LastIndexOf('/') + 1);
            return string.Compare(tidB, tidA, StringComparison.Ordinal);
        });
        
        return postKeys
            .Take(limit)
            .Select(key => ParseFeedItem(key, repo.GetRecord(key)!))
            .ToList();
    }

    /// <summary>
    /// Get records from a specific collection with optional limit.
    /// </summary>
    public static List<FeedItem> GetRecordsByCollection(MstRepository repo, string collection, int limit = 50)
    {
        var allKeys = repo.ListRecords();
        
        var collectionKeys = allKeys
            .Where(key => key.StartsWith($"{collection}/"))
            .ToList();
        
        // Sort by TID descending (newest first)
        collectionKeys.Sort((a, b) => 
        {
            string tidA = a.Substring(a.LastIndexOf('/') + 1);
            string tidB = b.Substring(b.LastIndexOf('/') + 1);
            return string.Compare(tidB, tidA, StringComparison.Ordinal);
        });
        
        return collectionKeys
            .Take(limit)
            .Select(key => ParseFeedItem(key, repo.GetRecord(key)!))
            .ToList();
    }
}

/// <summary>
/// Represents a feed item (post or repost) with extracted metadata.
/// </summary>
public class FeedItem
{
    /// <summary>
    /// The full repository path (e.g., "app.bsky.feed.post/3kj1abc").
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// The collection name (e.g., "app.bsky.feed.post").
    /// </summary>
    public string Collection { get; set; } = "";

    /// <summary>
    /// The TID (record key).
    /// </summary>
    public string Tid { get; set; } = "";

    /// <summary>
    /// The raw DAG-CBOR encoded record data.
    /// </summary>
    public byte[] RecordData { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// The record type (e.g., "app.bsky.feed.post", "app.bsky.feed.repost").
    /// </summary>
    public string? RecordType { get; set; }

    /// <summary>
    /// The createdAt timestamp from the record.
    /// </summary>
    public string? CreatedAt { get; set; }

    /// <summary>
    /// For posts: the text content.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// For reposts: the URI of the reposted item.
    /// </summary>
    public string? SubjectUri { get; set; }

    /// <summary>
    /// Get a friendly display of this feed item.
    /// </summary>
    public override string ToString()
    {
        if (RecordType == "app.bsky.feed.post")
        {
            return $"[{CreatedAt}] Post: {Text?.Substring(0, Math.Min(50, Text?.Length ?? 0))}...";
        }
        else if (RecordType == "app.bsky.feed.repost")
        {
            return $"[{CreatedAt}] Repost: {SubjectUri}";
        }
        else
        {
            return $"[{CreatedAt}] {RecordType}: {Path}";
        }
    }
}
