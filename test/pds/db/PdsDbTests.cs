
namespace dnproto.tests.pds.db;

using System.Reflection;
using dnproto.fs;
using dnproto.log;
using dnproto.pds.db;
using dnproto.repo;

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

    #region REPOHDR

    [Fact]
    public void RepoHeader_InsertAndRetrieve()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var repoHeaderToInsert = new DbRepoHeader
        {
            RepoCommitCid = CidV1.FromBase32("bafyreie5737gdxlw5i64vzichcalba3z2v5n6icifvx5xytvske7mr3hpm"),
            Version = Random.Shared.Next(1, 1000)
        };

        // Act
        pdsDb!.InsertUpdateRepoHeader(repoHeaderToInsert);

        var retrievedRepoHeader = pdsDb.GetRepoHeader();

        // Assert
        Assert.NotNull(retrievedRepoHeader);
        Assert.Equal(repoHeaderToInsert.RepoCommitCid?.GetBase32(), retrievedRepoHeader!.RepoCommitCid?.GetBase32());
        Assert.Equal(repoHeaderToInsert.Version, retrievedRepoHeader.Version);
    }

    [Fact]
    public void RepoHeader_Delete()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var repoHeaderToInsert = new DbRepoHeader
        {
            RepoCommitCid = CidV1.FromBase32("bafyreiahyzvpofpsudabba2mhjw62k5h6jtotsn7mt7ja7ams5sjqdpbai"),
            Version = Random.Shared.Next(1, 1000)
        };

        // Act
        pdsDb!.InsertUpdateRepoHeader(repoHeaderToInsert);

        var retrievedBeforeDelete = pdsDb.GetRepoHeader();
        Assert.NotNull(retrievedBeforeDelete);

        pdsDb.DeleteRepoHeader();

        var retrievedAfterDelete = pdsDb.GetRepoHeader();

        // Assert
        Assert.Null(retrievedAfterDelete);
    }

    #endregion


    #region REPOCMMT

    [Fact]
    public void RepoCommit_InsertAndRetrieve()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var repoCommitToInsert = new DbRepoCommit
        {
            Cid = CidV1.FromBase32("bafyreiahyzvpofpsudabba2mhjw62k5h6jtotsn7mt7ja7ams5sjqdpbai"),
            RootMstNodeCid = CidV1.FromBase32("bafyreie5737gdxlw5i64vzichcalba3z2v5n6icifvx5xytvske7mr3hpm"),
            Rev = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            Signature = Guid.NewGuid().ToByteArray(),
            Version = 3,
            PrevMstNodeCid = null
        };

        // Act
        pdsDb!.InsertUpdateRepoCommit(repoCommitToInsert);

        var retrievedRepoCommit = pdsDb.GetRepoCommit();

        // Assert
        Assert.NotNull(retrievedRepoCommit);
        Assert.Equal(repoCommitToInsert.Cid.Base32, retrievedRepoCommit?.Cid?.Base32);
        Assert.Equal(repoCommitToInsert.RootMstNodeCid.Base32, retrievedRepoCommit?.RootMstNodeCid?.Base32);
        Assert.Equal(repoCommitToInsert.Rev, retrievedRepoCommit?.Rev);
        Assert.Equal(repoCommitToInsert.Signature, retrievedRepoCommit?.Signature);
        Assert.Equal(repoCommitToInsert.Version, retrievedRepoCommit?.Version);
        Assert.Equal(repoCommitToInsert.PrevMstNodeCid?.Base32, retrievedRepoCommit?.PrevMstNodeCid?.Base32);
    }

    [Fact]
    public void RepoCommit_Delete()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var repoCommitToInsert = new DbRepoCommit
        {
            Cid = CidV1.FromBase32("bafyreiahyzvpofpsudabba2mhjw62k5h6jtotsn7mt7ja7ams5sjqdpbai"),
            RootMstNodeCid = CidV1.FromBase32("bafyreie5737gdxlw5i64vzichcalba3z2v5n6icifvx5xytvske7mr3hpm"),
            Rev = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            Signature = Guid.NewGuid().ToByteArray(),
            Version = 3,
            PrevMstNodeCid = null
        };

        // Act
        pdsDb!.InsertUpdateRepoCommit(repoCommitToInsert);

        var retrievedBeforeDelete = pdsDb.GetRepoCommit();
        Assert.NotNull(retrievedBeforeDelete);

        pdsDb.DeleteRepoCommit();

        var retrievedAfterDelete = pdsDb.GetRepoCommit();

        // Assert
        Assert.Null(retrievedAfterDelete);
    }

    #endregion


    #region MSTNODE
    [Fact]
    public void MstNode_InsertAndRetrieve()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstNodeToInsert = new DbMstNode
        {
            Cid = CidV1.FromBase32("bafyreie5737gdxlw5i64vzichcalba3z2v5n6icifvx5xytvske7mr3hpm"),
            LeftMstNodeCid = null
        };

        // Act
        pdsDb!.InsertMstNode(mstNodeToInsert);

        var retrievedMstNode = pdsDb.GetMstNode(mstNodeToInsert.Cid);

        // Assert
        Assert.NotNull(retrievedMstNode);
        Assert.Equal(mstNodeToInsert.Cid.Base32, retrievedMstNode!.Cid?.Base32);
        Assert.Equal(mstNodeToInsert.LeftMstNodeCid?.Base32, retrievedMstNode.LeftMstNodeCid?.Base32);

        pdsDb.DeleteMstNode(mstNodeToInsert.Cid);
    }

    [Fact]
    public void MstNode_InsertAndRetrieve_WithLeftMstNodeCid()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstNodeToInsert = new DbMstNode
        {
            Cid = CidV1.FromBase32("bafyreie5737gdxlw5i64vzichcalba3z2v5n6icifvx5xytvske7mr3hpm"),
            LeftMstNodeCid = CidV1.FromBase32("bafyreiahyzvpofpsudabba2mhjw62k5h6jtotsn7mt7ja7ams5sjqdpbai")
        };

        // Act
        pdsDb!.InsertMstNode(mstNodeToInsert);

        var retrievedMstNode = pdsDb.GetMstNode(mstNodeToInsert.Cid);

        // Assert
        Assert.NotNull(retrievedMstNode);
        Assert.Equal(mstNodeToInsert.Cid?.Base32, retrievedMstNode!.Cid?.Base32);
        Assert.Equal(mstNodeToInsert.LeftMstNodeCid?.Base32, retrievedMstNode.LeftMstNodeCid?.Base32);


        pdsDb.DeleteMstNode(mstNodeToInsert.Cid);
    }


    [Fact]
    public void MstNode_DeleteMstNode()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstNodeToInsert = new DbMstNode
        {
            Cid = CidV1.FromBase32("bafyreie5737gdxlw5i64vzichcalba3z2v5n6icifvx5xytvske7mr3hpm"),
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

        var mstNode1 = new DbMstNode
        {
            Cid = CidV1.FromBase32("bafyreifysqafipni5pe6dcxprngm3kybg5cyn5c4szstz6iedysdrcwjdm"),
            LeftMstNodeCid = null
        };

        var mstNode2 = new DbMstNode
        {
            Cid = CidV1.FromBase32("bafyreiahyzvpofpsudabba2mhjw62k5h6jtotsn7mt7ja7ams5sjqdpbai"),
            LeftMstNodeCid = mstNode1.Cid
        };

        // Act
        pdsDb!.InsertMstNode(mstNode1);
        pdsDb.InsertMstNode(mstNode2);

        var retrievedMstNode1 = pdsDb.GetMstNode(mstNode1.Cid);
        var retrievedMstNode2 = pdsDb.GetMstNode(mstNode2.Cid);

        // Assert
        Assert.NotNull(retrievedMstNode1);
        Assert.Equal(mstNode1.Cid?.Base32, retrievedMstNode1!.Cid?.Base32);

        Assert.NotNull(retrievedMstNode2);
        Assert.Equal(mstNode2.Cid?.Base32, retrievedMstNode2!.Cid?.Base32);
        Assert.Equal(mstNode1.Cid?.Base32, retrievedMstNode2.LeftMstNodeCid?.Base32);


        pdsDb.DeleteMstNode(mstNode1.Cid);
        pdsDb.DeleteMstNode(mstNode2.Cid);
    }

    [Fact]
    public void MstNode_DeleteAll()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstNode1 = new DbMstNode
        {
            Cid = CidV1.FromBase32("bafyreifysqafipni5pe6dcxprngm3kybg5cyn5c4szstz6iedysdrcwjdm"),
            LeftMstNodeCid = null
        };

        var mstNode2 = new DbMstNode
        {
            Cid = CidV1.FromBase32("bafyreiahyzvpofpsudabba2mhjw62k5h6jtotsn7mt7ja7ams5sjqdpbai"),
            LeftMstNodeCid = mstNode1.Cid
        };

        // Act
        pdsDb!.InsertMstNode(mstNode1);
        pdsDb.InsertMstNode(mstNode2);

        pdsDb.DeleteAllMstNodes();

        var retrievedMstNode1 = pdsDb.GetMstNode(mstNode1.Cid);
        var retrievedMstNode2 = pdsDb.GetMstNode(mstNode2.Cid);

        // Assert
        Assert.Null(retrievedMstNode1);
        Assert.Null(retrievedMstNode2);
    }

    [Fact]
    public void MstNode_InsertNodeWithOneEntry()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;
        var nodeCid = Guid.NewGuid().ToString();

        var mstNodeToInsert = new DbMstNode
        {
            Cid = CidV1.FromBase32("bafyreiahyzvpofpsudabba2mhjw62k5h6jtotsn7mt7ja7ams5sjqdpbai"),
            LeftMstNodeCid = null,
            Entries = new List<DbMstEntry>
            {
                new DbMstEntry
                {
                    KeySuffix = "exampleKey",
                    PrefixLength = 0,
                    TreeMstNodeCid = null,
                    RecordCid = CidV1.FromBase32("bafyreifysqafipni5pe6dcxprngm3kybg5cyn5c4szstz6iedysdrcwjdm")
                }
            }
        };

        // Act
        pdsDb!.InsertMstNode(mstNodeToInsert);

        var retrievedMstNode = pdsDb.GetMstNode(mstNodeToInsert.Cid);

        // Assert
        Assert.NotNull(retrievedMstNode);
        Assert.Equal(mstNodeToInsert.Cid?.Base32, retrievedMstNode!.Cid?.Base32);
        Assert.Single(retrievedMstNode.Entries);
        Assert.Equal("exampleKey", retrievedMstNode.Entries[0].KeySuffix);

        pdsDb.DeleteMstNode(mstNodeToInsert.Cid);
}


    [Fact]
    public void MstNode_InsertNodeWithTwoEntries()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;
        var nodeCid = Guid.NewGuid().ToString();

        var mstNodeToInsert = new DbMstNode
        {
            Cid = CidV1.FromBase32("bafyreifysqafipni5pe6dcxprngm3kybg5cyn5c4szstz6iedysdrcwjdm"),
            LeftMstNodeCid = null,
            Entries = new List<DbMstEntry>
            {
                new DbMstEntry
                {
                    KeySuffix = "exampleKey1",
                    PrefixLength = 0,
                    TreeMstNodeCid = null,
                    RecordCid = CidV1.FromBase32("bafyreifysqafipni5pe6dcxprngm3kybg5cyn5c4szstz6iedysdrcwjdm")
                },
                new DbMstEntry
                {
                    KeySuffix = "ampleKey2",
                    PrefixLength = 2,
                    TreeMstNodeCid = CidV1.FromBase32("bafyreifjef7rncdlfq347oislx3qiss2gt5jydzquzpjpwye6tsdf4joom"),
                    RecordCid = CidV1.FromBase32("bafyreiagh3ukdhtq2onx3pz2quesxvq5a4ucaqywvtqyjabqpkmibre7p4")
                }
            }
        };

        // Act
        pdsDb!.InsertMstNode(mstNodeToInsert);

        var retrievedMstNode = pdsDb.GetMstNode(mstNodeToInsert.Cid);

        // Assert
        Assert.NotNull(retrievedMstNode);
        Assert.Equal(mstNodeToInsert.Cid?.Base32, retrievedMstNode!.Cid?.Base32);
        Assert.Equal(2, retrievedMstNode.Entries.Count);
        Assert.Equal("exampleKey1", retrievedMstNode.Entries[0].KeySuffix);
        Assert.Equal("ampleKey2", retrievedMstNode.Entries[1].KeySuffix);
        Assert.Equal(2, retrievedMstNode.Entries[1].PrefixLength);
        Assert.Equal(0, retrievedMstNode.Entries[0].PrefixLength);

        pdsDb.DeleteMstNode(mstNodeToInsert.Cid);

    }

    #endregion





    #region REPORECORD

    [Fact]
    public void RepoRecord_InsertAndRetrieve()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var repoRecordToInsert = new DbRepoRecord
        {
            Cid = CidV1.FromBase32("bafyreifjef7rncdlfq347oislx3qiss2gt5jydzquzpjpwye6tsdf4joom"),
            DagCborObject = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data" } }
                }
            }
        };

        // Act
        pdsDb!.InsertRepoRecord(repoRecordToInsert);

        var retrievedRepoRecord = pdsDb.GetRepoRecord(repoRecordToInsert.Cid);

        // Assert
        Assert.NotNull(retrievedRepoRecord);
        Assert.Equal(repoRecordToInsert.Cid?.Base32, retrievedRepoRecord!.Cid?.Base32);
        Assert.Equal(repoRecordToInsert.DagCborObject?.ToString(), retrievedRepoRecord.DagCborObject?.ToString());

        pdsDb.DeleteRepoRecord(repoRecordToInsert.Cid);
    }

    [Fact]
    public void RepoRecord_InsertAndDelete()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var repoRecordToInsert = new DbRepoRecord
        {
            Cid = CidV1.FromBase32("bafyreifjef7rncdlfq347oislx3qiss2gt5jydzquzpjpwye6tsdf4joom"),
            DagCborObject = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data" } }
                }
            }
        };

        // Act
        pdsDb!.InsertRepoRecord(repoRecordToInsert);

        var retrievedBeforeDelete = pdsDb.GetRepoRecord(repoRecordToInsert.Cid);
        Assert.NotNull(retrievedBeforeDelete);
        Assert.Equal(repoRecordToInsert.Cid?.Base32, retrievedBeforeDelete!.Cid?.Base32);

        pdsDb.DeleteRepoRecord(repoRecordToInsert.Cid);

        var retrievedAfterDelete = pdsDb.GetRepoRecord(repoRecordToInsert.Cid);

        // Assert
        Assert.Null(retrievedAfterDelete);
    }

    [Fact]
    public void RepoRecord_DeleteAll()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var repoRecord1 = new DbRepoRecord
        {
            Cid = CidV1.FromBase32("bafyreifjef7rncdlfq347oislx3qiss2gt5jydzquzpjpwye6tsdf4joom"),
            DagCborObject = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data" } }
                }
            }
        };
        var repoRecord2 = new DbRepoRecord
        {
            Cid = CidV1.FromBase32("bafyreifysqafipni5pe6dcxprngm3kybg5cyn5c4szstz6iedysdrcwjdm"),
            DagCborObject = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data" } }
                }
            }
        };

        // Act
        pdsDb!.InsertRepoRecord(repoRecord1);
        pdsDb.InsertRepoRecord(repoRecord2);

        pdsDb.DeleteAllRepoRecords();

        var retrievedAfterDelete1 = pdsDb.GetRepoRecord(repoRecord1.Cid);
        var retrievedAfterDelete2 = pdsDb.GetRepoRecord(repoRecord2.Cid);

        // Assert
        Assert.Null(retrievedAfterDelete1);
        Assert.Null(retrievedAfterDelete2);
    }
    #endregion
}
