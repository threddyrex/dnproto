

namespace dnproto.tests.repo;


using dnproto.repo;

public class MstTests
{
    [Fact]
    public void KeyExists_True()
    {
        // Arrange - Create MST nodes and entries
        var mstNodes = new List<MstNode>
        {
            new MstNode
            {
                Cid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6"),
                LeftMstNodeCid = null
                
        }
        };
        var mstEntries = new List<MstEntry>
        {
            new MstEntry
            {
                MstNodeCid = mstNodes[0].Cid,
                KeySuffix = "app.bsky.actor.profile/self",
                PrefixLength = 0,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6")
            }
        };

        var mst = new Mst(CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6"), mstNodes, mstEntries);

        // Act
        bool exists = mst.KeyExists("app.bsky.actor.profile/self");
        bool doesntExist = mst.KeyExists("app.bsky.actor.profile/other");

        // Assert
        Assert.True(exists);
        Assert.False(doesntExist);
    }

    [Fact]
    public void KeyExists_WithPrefix()
    {
        // Arrange - Create MST nodes and entries
        var mstNodes = new List<MstNode>
        {
            new MstNode
            {
                Cid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6"),
                LeftMstNodeCid = null
                
        }
        };
        var mstEntries = new List<MstEntry>
        {
            new MstEntry
            {
                MstNodeCid = mstNodes[0].Cid,
                EntryIndex = 0,
                KeySuffix = "app.bsky.actor.profile/self",
                PrefixLength = 0,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6")
            },
            new MstEntry
            {
                MstNodeCid = mstNodes[0].Cid,
                EntryIndex = 1,
                KeySuffix = "other",
                PrefixLength = 23,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6")
            }
        };

        var mst = new Mst(mstNodes[0].Cid!, mstNodes, mstEntries);

        // Act
        bool exists = mst.KeyExists("app.bsky.actor.profile/other");

        // Assert
        Assert.True(exists);

    }


    [Fact]
    public void GetKeyDepth()
    {
        Assert.Equal(0, Mst.GetKeyDepth("2653ae71"));
        Assert.Equal(1, Mst.GetKeyDepth("blue"));
        Assert.Equal(4, Mst.GetKeyDepth("app.bsky.feed.post/454397e440ec"));
        Assert.Equal(8, Mst.GetKeyDepth("app.bsky.feed.post/9adeb165882c"));
    }
}