

namespace dnproto.tests.pds;


using dnproto.fs;
using dnproto.log;
using dnproto.pds;
using dnproto.repo;




public class MstTestsFixture : IDisposable
{
    public Logger Logger { get; set; } = new Logger();
    public LocalFileSystem Lfs { get; set; }

    public PdsDb? PdsDb { get; set; }

    public MstTestsFixture()
    {
        Logger.AddDestination(new ConsoleLogDestination());
        string tempDir = Path.Combine(Path.GetTempPath(), "mst-tests-data-dir");
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

        Installer.InstallDb(Lfs, Logger, deleteExistingDb: false);
        PdsDb = PdsDb.ConnectPdsDb(Lfs, Logger);
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
        var mstNode = new RepoMstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6"),
            LeftMstNodeCid = null
        };

        _fixture.PdsDb.InsertMstNode(mstNode);

        _fixture.PdsDb.InsertMstEntry((Guid)mstNode.NodeObjectId, new RepoMstEntry
            {
                KeySuffix = "app.bsky.actor.profile/self",
                PrefixLength = 0,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6")
            });


        // Act
        var mst = MstDb.ConnectMstDb(_fixture.Lfs, _fixture.Logger, _fixture.PdsDb);
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
        var mstNode =new RepoMstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6"),
            LeftMstNodeCid = null                
        }
        ;
        _fixture.PdsDb.InsertMstNode(mstNode);


        var mstEntries = new List<RepoMstEntry>
        {
            new RepoMstEntry
            {
                EntryIndex = 0,
                KeySuffix = "app.bsky.actor.profile/self",
                PrefixLength = 0,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6")
            },
            new RepoMstEntry
            {
                EntryIndex = 1,
                KeySuffix = "other",
                PrefixLength = 23,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreia67z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6z7x2f5t3g5x7z5q4y6")
            }
        };
        _fixture.PdsDb.InsertMstEntries((Guid)mstNode.NodeObjectId, mstEntries);

        var mst = MstDb.ConnectMstDb(_fixture.Lfs, _fixture.Logger, _fixture.PdsDb);

        // Act
        bool exists = mst.KeyExists("app.bsky.actor.profile/other");

        // Assert
        Assert.True(exists);

    }



}