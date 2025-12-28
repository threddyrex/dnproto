
namespace dnproto.tests.pds.db;

using System.Reflection;
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

    [Fact]
    public void MstNode_InsertAndRetrieve()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstNodeToInsert = new MstNode
        {
            Cid = Guid.NewGuid().ToString(),
            LeftMstNodeCid = null
        };

        // Act
        pdsDb!.InsertMstNode(mstNodeToInsert);

        var retrievedMstNode = pdsDb.GetMstNode(mstNodeToInsert.Cid);

        // Assert
        Assert.NotNull(retrievedMstNode);
        Assert.Equal(mstNodeToInsert.Cid, retrievedMstNode!.Cid);
        Assert.Equal(mstNodeToInsert.LeftMstNodeCid, retrievedMstNode.LeftMstNodeCid);
    }

    [Fact]
    public void MstNode_InsertAndRetrieve_WithLeftMstNodeCid()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstNodeToInsert = new MstNode
        {
            Cid = Guid.NewGuid().ToString(),
            LeftMstNodeCid = Guid.NewGuid().ToString()
        };

        // Act
        pdsDb!.InsertMstNode(mstNodeToInsert);

        var retrievedMstNode = pdsDb.GetMstNode(mstNodeToInsert.Cid);

        // Assert
        Assert.NotNull(retrievedMstNode);
        Assert.Equal(mstNodeToInsert.Cid, retrievedMstNode!.Cid);
        Assert.Equal(mstNodeToInsert.LeftMstNodeCid, retrievedMstNode.LeftMstNodeCid);
    }


    [Fact]
    public void MstNode_DeleteMstNode()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstNodeToInsert = new MstNode
        {
            Cid = Guid.NewGuid().ToString(),
            LeftMstNodeCid = null
        };

        // Act
        pdsDb!.InsertMstNode(mstNodeToInsert);

        var retrievedMstNode = pdsDb.GetMstNode(mstNodeToInsert.Cid);
        Assert.NotNull(retrievedMstNode);

        pdsDb.DeleteMstNode(mstNodeToInsert.Cid);

        var retrievedAfterDelete = pdsDb.GetMstNode(mstNodeToInsert.Cid);

        // Assert
        Assert.Null(retrievedAfterDelete);
    }

    [Fact]
    public void MstNode_InsertTwo()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstNode1 = new MstNode
        {
            Cid = Guid.NewGuid().ToString(),
            LeftMstNodeCid = null
        };

        var mstNode2 = new MstNode
        {
            Cid = Guid.NewGuid().ToString(),
            LeftMstNodeCid = mstNode1.Cid
        };

        // Act
        pdsDb!.InsertMstNode(mstNode1);
        pdsDb.InsertMstNode(mstNode2);

        var retrievedMstNode1 = pdsDb.GetMstNode(mstNode1.Cid);
        var retrievedMstNode2 = pdsDb.GetMstNode(mstNode2.Cid);

        // Assert
        Assert.NotNull(retrievedMstNode1);
        Assert.Equal(mstNode1.Cid, retrievedMstNode1!.Cid);

        Assert.NotNull(retrievedMstNode2);
        Assert.Equal(mstNode2.Cid, retrievedMstNode2!.Cid);
        Assert.Equal(mstNode1.Cid, retrievedMstNode2.LeftMstNodeCid);
    }

    [Fact]
    public void MstEntry_InsertAndRetrieve()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstEntryToInsert = new MstEntry
        {
            MstNodeCid = Guid.NewGuid().ToString(),
            KeySuffix = "example.key/suffix",
            PrefixLength = 5,
            TreeMstNodeCid = null,
            RecordCid = Guid.NewGuid().ToString()
        };

        // Act
        pdsDb!.InsertMstEntry(mstEntryToInsert);

        var retrievedMstEntry = pdsDb.GetMstEntriesForNode(mstEntryToInsert.MstNodeCid);

        // Assert
        Assert.NotNull(retrievedMstEntry);
        Assert.Single(retrievedMstEntry);
        var entry = retrievedMstEntry[0];
        Assert.Equal(mstEntryToInsert.MstNodeCid, entry.MstNodeCid);
        Assert.Equal(mstEntryToInsert.KeySuffix, entry.KeySuffix);
        Assert.Equal(mstEntryToInsert.PrefixLength, entry.PrefixLength);
        Assert.Equal(mstEntryToInsert.TreeMstNodeCid, entry.TreeMstNodeCid);
        Assert.Equal(mstEntryToInsert.RecordCid, entry.RecordCid);
    }

    [Fact]
    public void MstEntry_InsertTwoEntriesSameNode()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstNodeCid = Guid.NewGuid().ToString();

        var mstEntry1 = new MstEntry
        {
            MstNodeCid = mstNodeCid,
            KeySuffix = "first.entry",
            PrefixLength = 0,
            TreeMstNodeCid = null,
            RecordCid = Guid.NewGuid().ToString()
        };

        var mstEntry2 = new MstEntry
        {
            MstNodeCid = mstNodeCid,
            KeySuffix = "second.entry",
            PrefixLength = 5,
            TreeMstNodeCid = null,
            RecordCid = Guid.NewGuid().ToString()
        };

        // Act
        pdsDb!.InsertMstEntry(mstEntry1);
        pdsDb.InsertMstEntry(mstEntry2);

        var retrievedMstEntries = pdsDb.GetMstEntriesForNode(mstNodeCid);

        // Assert
        Assert.NotNull(retrievedMstEntries);
        Assert.Equal(2, retrievedMstEntries.Count);
        var entry1 = retrievedMstEntries[0];
        Assert.Equal(mstEntry1.MstNodeCid, entry1.MstNodeCid);
        Assert.Equal(mstEntry1.KeySuffix, entry1.KeySuffix);
        Assert.Equal(mstEntry1.PrefixLength, entry1.PrefixLength);
    }

    [Fact]
    public void MstEntry_InsertAndDelete()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstEntryToInsert = new MstEntry
        {
            MstNodeCid = Guid.NewGuid().ToString(),
            KeySuffix = "to.be.deleted",
            PrefixLength = 0,
            TreeMstNodeCid = null,
            RecordCid = Guid.NewGuid().ToString()
        };

        // Act
        pdsDb!.InsertMstEntry(mstEntryToInsert);

        var retrievedBeforeDelete = pdsDb.GetMstEntriesForNode(mstEntryToInsert.MstNodeCid);
        Assert.NotNull(retrievedBeforeDelete);
        Assert.Single(retrievedBeforeDelete);
        Assert.Equal(mstEntryToInsert.KeySuffix, retrievedBeforeDelete[0].KeySuffix);

        pdsDb.DeleteMstEntriesForNode(mstEntryToInsert.MstNodeCid);

        var retrievedAfterDelete = pdsDb.GetMstEntriesForNode(mstEntryToInsert.MstNodeCid);

        // Assert
        Assert.NotNull(retrievedAfterDelete);
        Assert.Empty(retrievedAfterDelete);
    }

}
