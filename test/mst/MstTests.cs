using dnproto.mst;

namespace dnproto.tests.mst;


public class MstTests
{
    [Fact]
    public void AssembleTree_OneItem()
    {
        // Arrange
        var items = new List<MstItem>
        {
            new MstItem { Key = "key1", Value = "value1" }
        };

        // Act
        var tree = Mst.AssembleTreeFromItems(items, new dnproto.log.Logger());

        // Assert
        Assert.NotNull(tree);
        Assert.NotNull(tree.Root);
        Assert.Single(tree.Root.Entries);
        Assert.Equal("key1", tree.Root.Entries[0].Key);
        Assert.Equal("value1", tree.Root.Entries[0].Value);
        Assert.Equal(Mst.GetKeyDepth("key1"), tree.Root.KeyDepth);
    }

    [Fact]
    public void AssembleTree_TwoItems()
    {
        // Arrange
        var items = new List<MstItem>
        {
            new MstItem { Key = "key1", Value = "value1" },
            new MstItem { Key = "key2", Value = "value2" }
        };

        // Act
        var tree = Mst.AssembleTreeFromItems(items, new dnproto.log.Logger());

        // Assert
        Assert.NotNull(tree);
        Assert.NotNull(tree.Root);
        Assert.Equal(2, tree.Root.Entries.Count);
        Assert.Equal("key1", tree.Root.Entries[0].Key);
        Assert.Equal("value1", tree.Root.Entries[0].Value);
        Assert.Equal("key2", tree.Root.Entries[1].Key);
        Assert.Equal("value2", tree.Root.Entries[1].Value);
        Assert.Equal(Mst.GetKeyDepth("key1"), tree.Root.KeyDepth);
    }
}