namespace dnproto.tests.repo;

using dnproto.repo;

public class MstEntryTests
{
    [Fact]
    public void GetFullKey_First()
    {
        // Arrange
        string previousKey = "";

        RepoMstEntry entry = new RepoMstEntry
        {
            EntryIndex = 0,
            KeySuffix = "app.bsky.actor.profile/self",
            PrefixLength = 0
        };

        // Act
        string fullKey = entry.GetFullKey(previousKey);

        // Assert
        Assert.Equal("app.bsky.actor.profile/self", fullKey);
    }

    [Fact]
    public void GetFullKey_SecondEntry()
    {
        // Arrange
        string previousKey = "app.bsky.feed.like/3ma2awdxgy22k";

        RepoMstEntry entry = new RepoMstEntry
        {
            EntryIndex = 1,
            KeySuffix = "post/3kjcfuuylv624",
            PrefixLength = 14
        };

        // Act
        string fullKey = entry.GetFullKey(previousKey);

        // Assert
        Assert.Equal("app.bsky.feed.post/3kjcfuuylv624", fullKey);
    }



    [Fact]
    public void GetFullKeys()
    {
        // Arrange
        List<RepoMstEntry> entries = new List<RepoMstEntry>
        {
            new RepoMstEntry
            {
                EntryIndex = 0,
                KeySuffix = "app.bsky.feed.like/3ma2awdxgy22k",
                PrefixLength = 0
            },
            new RepoMstEntry
            {
                EntryIndex = 1,
                KeySuffix = "post/3kjcfuuylv624",
                PrefixLength = 14
            },
            new RepoMstEntry
            {
                EntryIndex = 2,
                KeySuffix = "graph.follow/3las5ynsp4u2t",
                PrefixLength = 9
            }
        };

        // Act
        List<string> fullKeys = RepoMstEntry.GetFullKeys(entries);
        // Assert
        Assert.Equal(3, fullKeys.Count);
        Assert.Equal("app.bsky.feed.like/3ma2awdxgy22k", fullKeys[0]);
        Assert.Equal("app.bsky.feed.post/3kjcfuuylv624", fullKeys[1]);
        Assert.Equal("app.bsky.graph.follow/3las5ynsp4u2t", fullKeys[2]);
    }


    [Fact]
    public void FixEntryIndices()
    {

        // Arrange - Create MST node and entries
        var mstNode = new RepoMstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6"),
            LeftMstNodeCid = null
        };

        var mstEntries = new List<RepoMstEntry>
        {
            new RepoMstEntry
            {
                EntryIndex = 0,
                KeySuffix = "apple",
                PrefixLength = 0,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6")
            },
            new RepoMstEntry
            {
                EntryIndex = 1,
                KeySuffix = "banana",
                PrefixLength = 0,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6")
            }
        };

        mstEntries[0].EntryIndex = 6;
        mstEntries[1].EntryIndex = 7;

        RepoMstEntry.FixEntryIndexes(mstEntries);
        Assert.Equal(0, mstEntries[0].EntryIndex);
        Assert.Equal(1, mstEntries[1].EntryIndex);

    }

    [Fact]
    public void GetCommonPrefixLength_Test()
    {
        Assert.Equal(0, RepoMstEntry.GetCommonPrefixLength("apple", "banana"));
        Assert.Equal(3, RepoMstEntry.GetCommonPrefixLength("application", "appetite"));
        Assert.Equal(5, RepoMstEntry.GetCommonPrefixLength("hello", "hello"));
        Assert.Equal(0, RepoMstEntry.GetCommonPrefixLength("short", "longer"));
        Assert.Equal(6, RepoMstEntry.GetCommonPrefixLength("prefixes", "prefixation"));
    }



    [Fact]
    public void GetKeyDepth()
    {
        Assert.Equal(0, RepoMstEntry.GetKeyDepth("2653ae71"));
        Assert.Equal(1, RepoMstEntry.GetKeyDepth("blue"));
        Assert.Equal(4, RepoMstEntry.GetKeyDepth("app.bsky.feed.post/454397e440ec"));
        Assert.Equal(8, RepoMstEntry.GetKeyDepth("app.bsky.feed.post/9adeb165882c"));
    }


    [Fact]
    public void CompareKeys()
    {
        Assert.Equal(0, RepoMstEntry.CompareKeys("apple", "apple"));
        Assert.True(RepoMstEntry.CompareKeys("apple", "banana") < 0);
        Assert.True(RepoMstEntry.CompareKeys("banana", "apple") > 0);
        Assert.True(RepoMstEntry.CompareKeys("app", "apple") < 0);
        Assert.True(RepoMstEntry.CompareKeys("apple", "app") > 0);
    }

}