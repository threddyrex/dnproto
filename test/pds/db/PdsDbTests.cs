
namespace dnproto.tests.pds.db;

using dnproto.fs;
using dnproto.log;
using dnproto.pds.db;

public class PdsDbTestsFixture : IDisposable
{
    Logger Logger { get; set; } = new Logger();
    public LocalFileSystem? Lfs { get; set; }

    public PdsDb? PdsDb { get; set; }

    public PdsDbTestsFixture()
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

        PdsDb = PdsDb.InitializePdsDb(tempDir, Logger);
    }

    public void Dispose()
    {
    }
}


public class PdsDbTests : IClassFixture<PdsDbTestsFixture>
{
    private readonly PdsDbTestsFixture _fixture;

    public PdsDbTests(PdsDbTestsFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void PdsDb_Initialize_CreatesDatabaseFile()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;
        var lfs = _fixture.Lfs;

        // Act
        string pdsDbFilePath = Path.Combine(lfs!.DataDir, "pds", "pds.db");
        bool dbFileExists = File.Exists(pdsDbFilePath);

        // Assert
        Assert.NotNull(pdsDb);
        Assert.True(dbFileExists);
    }


    [Fact]
    public void RepoHeader_InsertAndRetrieve()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var repoHeaderToInsert = new RepoHeader
        {
            RepoCommitCid = Guid.NewGuid().ToString(),
            Version = Random.Shared.Next(1, 1000)
        };

        // Act
        pdsDb!.InsertUpdateRepoHeader(repoHeaderToInsert);

        var retrievedRepoHeader = pdsDb.GetRepoHeader();

        // Assert
        Assert.NotNull(retrievedRepoHeader);
        Assert.Equal(repoHeaderToInsert.RepoCommitCid, retrievedRepoHeader!.RepoCommitCid);
        Assert.Equal(repoHeaderToInsert.Version, retrievedRepoHeader.Version);
    }

    [Fact]
    public void RepoCommit_InsertAndRetrieve()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var repoCommitToInsert = new RepoCommit
        {
            Cid = Guid.NewGuid().ToString(),
            RootMstNodeCid = Guid.NewGuid().ToString(),
            Rev = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            Signature = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            Version = 3,
            PrevMstNodeCid = null
        };

        // Act
        pdsDb!.InsertUpdateRepoCommit(repoCommitToInsert);

        var retrievedRepoCommit = pdsDb.GetRepoCommit();

        // Assert
        Assert.NotNull(retrievedRepoCommit);
        Assert.Equal(repoCommitToInsert.Cid, retrievedRepoCommit!.Cid);
        Assert.Equal(repoCommitToInsert.RootMstNodeCid, retrievedRepoCommit.RootMstNodeCid);
        Assert.Equal(repoCommitToInsert.Rev, retrievedRepoCommit.Rev);
        Assert.Equal(repoCommitToInsert.Signature, retrievedRepoCommit.Signature);
        Assert.Equal(repoCommitToInsert.Version, retrievedRepoCommit.Version);
        Assert.Equal(repoCommitToInsert.PrevMstNodeCid, retrievedRepoCommit.PrevMstNodeCid);
    }


}
