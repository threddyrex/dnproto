

namespace dnproto.tests.pds;


using dnproto.fs;
using dnproto.log;
using dnproto.pds;
using dnproto.pds.db;
using dnproto.repo;




public class MstTestsFixture : IDisposable
{
    Logger Logger { get; set; } = new Logger();
    public LocalFileSystem? Lfs { get; set; }

    public PdsDb? PdsDb { get; set; }

    public MstTestsFixture()
    {
        Logger.AddDestination(new ConsoleLogDestination());
        string tempDir = Path.Combine(Path.GetTempPath(), "dnproto-tests-data-dir");
        Logger.LogInfo($"Using temp dir for tests: {tempDir}");

        if(!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);            
        }

        Lfs = LocalFileSystem.Initialize(tempDir, Logger);

        string pdsDbFile = Path.Combine(tempDir, "pds", "pds.db");
        Logger.LogInfo($"PDS database file path: {pdsDbFile}");
        if (File.Exists(pdsDbFile))
        {
            File.Delete(pdsDbFile);
        }

        PdsDb = PdsDb.InstallPdsDb(tempDir, Logger);
    }

    public void Dispose()
    {
    }
}



public class MstTests : IClassFixture<MstTestsFixture>
{
    private readonly MstTestsFixture _fixture;

    public MstTests(MstTestsFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void KeyExists_True()
    {
        _fixture.PdsDb!.DeleteAllMstNodes();
        _fixture.PdsDb.DeleteAllMstEntries();

        // Arrange - Create MST nodes and entries
        var mstNode = new MstNode
        {
            Cid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6"),
            LeftMstNodeCid = null
        };

        _fixture.PdsDb.InsertMstNode(mstNode);

        _fixture.PdsDb.InsertMstEntry(mstNode.Cid, new MstEntry
            {
                MstNodeCid = mstNode.Cid,
                KeySuffix = "app.bsky.actor.profile/self",
                PrefixLength = 0,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6")
            });


        // Act
        var mst = new Mst(_fixture.PdsDb);
        bool exists = mst.KeyExists("app.bsky.actor.profile/self");
        bool doesntExist = mst.KeyExists("app.bsky.actor.profile/other");

        // Assert
        Assert.True(exists);
        Assert.False(doesntExist);
    }

    [Fact]
    public void KeyExists_WithPrefix()
    {
        _fixture.PdsDb!.DeleteAllMstNodes();
        _fixture.PdsDb.DeleteAllMstEntries();


        // Arrange - Create MST nodes and entries
        var mstNode =new MstNode
        {
            Cid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6"),
            LeftMstNodeCid = null                
        }
        ;
        _fixture.PdsDb.InsertMstNode(mstNode);


        var mstEntries = new List<MstEntry>
        {
            new MstEntry
            {
                MstNodeCid = mstNode.Cid,
                EntryIndex = 0,
                KeySuffix = "app.bsky.actor.profile/self",
                PrefixLength = 0,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6")
            },
            new MstEntry
            {
                MstNodeCid = mstNode.Cid,
                EntryIndex = 1,
                KeySuffix = "other",
                PrefixLength = 23,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6")
            }
        };
        _fixture.PdsDb.InsertMstEntries(mstNode.Cid, mstEntries);

        var mst = new Mst(_fixture.PdsDb);

        // Act
        bool exists = mst.KeyExists("app.bsky.actor.profile/other");

        // Assert
        Assert.True(exists);

    }


    [Fact]
    public void GetKeyDepth()
    {
        var mst = new Mst(_fixture.PdsDb!);
        Assert.Equal(0, mst.GetKeyDepth("2653ae71"));
        Assert.Equal(1, mst.GetKeyDepth("blue"));
        Assert.Equal(4, mst.GetKeyDepth("app.bsky.feed.post/454397e440ec"));
        Assert.Equal(8, mst.GetKeyDepth("app.bsky.feed.post/9adeb165882c"));
    }


    [Fact]
    public void CompareKeys()
    {
        var mst = new Mst(_fixture.PdsDb!);
        Assert.Equal(0, mst.CompareKeys("apple", "apple"));
        Assert.True(mst.CompareKeys("apple", "banana") < 0);
        Assert.True(mst.CompareKeys("banana", "apple") > 0);
        Assert.True(mst.CompareKeys("app", "apple") < 0);
        Assert.True(mst.CompareKeys("apple", "app") > 0);
    }


    [Fact]
    public void FixEntryIndices()
    {
        _fixture.PdsDb!.DeleteAllMstNodes();
        _fixture.PdsDb.DeleteAllMstEntries();

        // Arrange - Create MST node and entries
        var mstNode = new MstNode
        {
            Cid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6"),
            LeftMstNodeCid = null
        };

        _fixture.PdsDb.InsertMstNode(mstNode);

        var mstEntries = new List<MstEntry>
        {
            new MstEntry
            {
                MstNodeCid = mstNode.Cid,
                EntryIndex = 0,
                KeySuffix = "apple",
                PrefixLength = 0,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6")
            },
            new MstEntry
            {
                MstNodeCid = mstNode.Cid,
                EntryIndex = 1,
                KeySuffix = "banana",
                PrefixLength = 0,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6")
            }
        };
        _fixture.PdsDb.InsertMstEntries(mstNode.Cid, mstEntries);

        var mst = new Mst(_fixture.PdsDb);

        mstEntries[0].EntryIndex = 6;
        mstEntries[1].EntryIndex = 7;

        mst.FixEntryIndices(mstEntries);
        Assert.Equal(0, mstEntries[0].EntryIndex);
        Assert.Equal(1, mstEntries[1].EntryIndex);

    }
}