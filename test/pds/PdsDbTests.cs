
namespace dnproto.tests.pds;

using System.Reflection;
using dnproto.fs;
using dnproto.log;
using dnproto.pds;
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

        PdsDb = PdsDb.InstallPdsDb(tempDir, Logger);
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

    #region CONFIG

    [Fact]
    public void Config_InsertAndRetrieve()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var configToInsert = new DbConfig
        {
            ListenScheme = "http",
            ListenHost = "localhost",
            ListenPort = 8080,
            PdsDid = "did:example:123456789abcdefghi",
            PdsHostname = "example.com",
            AvailableUserDomain = "users.example.com",
            AdminHashedPassword = "hashed_admin_password",
            JwtSecret = "super_secret_jwt_key",
            UserHandle = "testuser",
            UserDid = "did:example:user123",
            UserHashedPassword = "hashed_user_password",
            UserEmail = "user@example.com",
            UserPublicKeyMultibase = "zPublicKeyMultibaseExample",
            UserPrivateKeyMultibase = "zPrivateKeyMultibaseExample"
        };
        // Act
        pdsDb!.InsertConfig(configToInsert);
        var retrievedConfig = pdsDb.GetConfig();
        int configCount = pdsDb.GetConfigCount();

        // Assert
        Assert.NotNull(retrievedConfig);
        Assert.Equal(configToInsert.ListenScheme, retrievedConfig!.ListenScheme);
        Assert.Equal(configToInsert.ListenHost, retrievedConfig.ListenHost);
        Assert.Equal(configToInsert.ListenPort, retrievedConfig.ListenPort);
        Assert.Equal(configToInsert.PdsDid, retrievedConfig.PdsDid);
        Assert.Equal(configToInsert.PdsHostname, retrievedConfig.PdsHostname);
        Assert.Equal(configToInsert.AvailableUserDomain, retrievedConfig.AvailableUserDomain);
        Assert.Equal(configToInsert.AdminHashedPassword, retrievedConfig.AdminHashedPassword);
        Assert.Equal(configToInsert.JwtSecret, retrievedConfig.JwtSecret);
        Assert.Equal(configToInsert.UserHandle, retrievedConfig.UserHandle);
        Assert.Equal(configToInsert.UserDid, retrievedConfig.UserDid);
        Assert.Equal(configToInsert.UserHashedPassword, retrievedConfig.UserHashedPassword);
        Assert.Equal(configToInsert.UserEmail, retrievedConfig.UserEmail);
        Assert.Equal(configToInsert.UserPublicKeyMultibase, retrievedConfig.UserPublicKeyMultibase);
        Assert.Equal(configToInsert.UserPrivateKeyMultibase, retrievedConfig.UserPrivateKeyMultibase);
        Assert.Equal(1, configCount);
    }

    #endregion


    #region REPOHDR

    [Fact]
    public void RepoHeader_InsertAndRetrieve()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var repoHeaderToInsert = new RepoHeader
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

        var repoHeaderToInsert = new RepoHeader
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

        var repoCommitToInsert = new RepoCommit
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

        var repoCommitToInsert = new RepoCommit
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

        var mstNodeToInsert = new MstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = CidV1.FromBase32("bafyreie5737gdxlw5i64vzichcalba3z2v5n6icifvx5xytvske7mr3hpm"),
            LeftMstNodeCid = null
        };

        // Act
        pdsDb!.InsertMstNode(mstNodeToInsert);

        var retrievedMstNode = pdsDb.GetMstNodeByCid(mstNodeToInsert.Cid);
        var retrievedMstNodeById = pdsDb.GetMstNodeByObjectId(mstNodeToInsert.NodeObjectId);

        // Assert
        Assert.NotNull(retrievedMstNode);
        Assert.Equal(mstNodeToInsert.Cid.Base32, retrievedMstNode!.Cid?.Base32);
        Assert.Equal(mstNodeToInsert.LeftMstNodeCid?.Base32, retrievedMstNode.LeftMstNodeCid?.Base32);

        Assert.NotNull(retrievedMstNodeById);
        Assert.Equal(mstNodeToInsert.Cid.Base32, retrievedMstNodeById!.Cid?.Base32);
        Assert.Equal(mstNodeToInsert.LeftMstNodeCid?.Base32, retrievedMstNodeById.LeftMstNodeCid?.Base32);
        Assert.Equal(mstNodeToInsert.NodeObjectId, retrievedMstNodeById.NodeObjectId);

        pdsDb.DeleteMstNodeByObjectId(mstNodeToInsert.NodeObjectId);
    }

    [Fact]
    public void MstNode_InsertAndRetrieve_WithLeftMstNodeCid()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstNodeToInsert = new MstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = CidV1.FromBase32("bafyreie5737gdxlw5i64vzichcalba3z2v5n6icifvx5xytvske7mr3hpm"),
            LeftMstNodeCid = CidV1.FromBase32("bafyreiahyzvpofpsudabba2mhjw62k5h6jtotsn7mt7ja7ams5sjqdpbai")
        };

        // Act
        pdsDb!.InsertMstNode(mstNodeToInsert);

        var retrievedMstNode = pdsDb.GetMstNodeByCid(mstNodeToInsert.Cid);
        var retrievedMstNodeById = pdsDb.GetMstNodeByObjectId(mstNodeToInsert.NodeObjectId);

        // Assert
        Assert.NotNull(retrievedMstNode);
        Assert.Equal(mstNodeToInsert.Cid?.Base32, retrievedMstNode!.Cid?.Base32);
        Assert.Equal(mstNodeToInsert.LeftMstNodeCid?.Base32, retrievedMstNode.LeftMstNodeCid?.Base32);

        Assert.NotNull(retrievedMstNodeById);
        Assert.Equal(mstNodeToInsert.Cid?.Base32, retrievedMstNodeById!.Cid?.Base32);
        Assert.Equal(mstNodeToInsert.LeftMstNodeCid?.Base32, retrievedMstNodeById.LeftMstNodeCid?.Base32);
        Assert.Equal(mstNodeToInsert.NodeObjectId, retrievedMstNodeById.NodeObjectId);


        pdsDb.DeleteMstNodeByObjectId(mstNodeToInsert.NodeObjectId);
    }


    [Fact]
    public void MstNode_DeleteMstNode()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstNodeToInsert = new MstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = CidV1.FromBase32("bafyreie5737gdxlw5i64vzichcalba3z2v5n6icifvx5xytvske7mr3hpm"),
            LeftMstNodeCid = null
        };

        // Act
        pdsDb!.InsertMstNode(mstNodeToInsert);

        var retrievedMstNode = pdsDb.GetMstNodeByCid(mstNodeToInsert.Cid);
        var retrievedMstNodeById = pdsDb.GetMstNodeByObjectId(mstNodeToInsert.NodeObjectId);

        Assert.NotNull(retrievedMstNode);
        Assert.NotNull(retrievedMstNodeById);

        pdsDb.DeleteMstNodeByObjectId(mstNodeToInsert.NodeObjectId);

        // Assert
        Assert.False(pdsDb.MstNodeExistsByCid(mstNodeToInsert.Cid));
    }

    [Fact]
    public void MstNode_InsertTwo()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstNode1 = new MstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = CidV1.FromBase32("bafyreifysqafipni5pe6dcxprngm3kybg5cyn5c4szstz6iedysdrcwjdm"),
            LeftMstNodeCid = null
        };

        var mstNode2 = new MstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = CidV1.FromBase32("bafyreiahyzvpofpsudabba2mhjw62k5h6jtotsn7mt7ja7ams5sjqdpbai"),
            LeftMstNodeCid = mstNode1.Cid
        };

        // Act
        pdsDb!.InsertMstNode(mstNode1);
        pdsDb.InsertMstNode(mstNode2);

        var retrievedMstNode1 = pdsDb.GetMstNodeByCid(mstNode1.Cid);
        var retrievedMstNode2 = pdsDb.GetMstNodeByCid(mstNode2.Cid);

        // Assert
        Assert.NotNull(retrievedMstNode1);
        Assert.Equal(mstNode1.Cid?.Base32, retrievedMstNode1!.Cid?.Base32);

        Assert.NotNull(retrievedMstNode2);
        Assert.Equal(mstNode2.Cid?.Base32, retrievedMstNode2!.Cid?.Base32);
        Assert.Equal(mstNode1.Cid?.Base32, retrievedMstNode2.LeftMstNodeCid?.Base32);


        pdsDb.DeleteMstNodeByObjectId(mstNode1.NodeObjectId);
        pdsDb.DeleteMstNodeByObjectId(mstNode2.NodeObjectId);
    }

    [Fact]
    public void MstNode_DeleteAll()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var mstNode1 = new MstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = CidV1.FromBase32("bafyreifysqafipni5pe6dcxprngm3kybg5cyn5c4szstz6iedysdrcwjdm"),
            LeftMstNodeCid = null
        };

        var mstNode2 = new MstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = CidV1.FromBase32("bafyreiahyzvpofpsudabba2mhjw62k5h6jtotsn7mt7ja7ams5sjqdpbai"),
            LeftMstNodeCid = mstNode1.Cid
        };

        // Act
        pdsDb!.InsertMstNode(mstNode1);
        pdsDb.InsertMstNode(mstNode2);

        pdsDb.DeleteAllMstNodes();

        // Assert
        Assert.False(pdsDb.MstNodeExistsByCid(mstNode1.Cid));
        Assert.False(pdsDb.MstNodeExistsByCid(mstNode2.Cid));
    }

    [Fact]
    public void MstNode_InsertNodeWithOneEntry()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;
        var nodeCid = Guid.NewGuid().ToString();

        var mstNodeToInsert = new MstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = CidV1.FromBase32("bafyreiahyzvpofpsudabba2mhjw62k5h6jtotsn7mt7ja7ams5sjqdpbai"),
            LeftMstNodeCid = null
        };
        var mstEntries = new List<MstEntry>
        {
            new MstEntry
            {
                EntryIndex = 0,
                KeySuffix = "exampleKey",
                PrefixLength = 0,
                TreeMstNodeCid = null,
                RecordCid = CidV1.FromBase32("bafyreifysqafipni5pe6dcxprngm3kybg5cyn5c4szstz6iedysdrcwjdm")
            }
        };


        // Act
        pdsDb!.InsertMstNode(mstNodeToInsert);
        pdsDb!.InsertMstEntries((Guid)mstNodeToInsert.NodeObjectId, mstEntries);

        var retrievedMstNode = pdsDb.GetMstNodeByObjectId((Guid)mstNodeToInsert.NodeObjectId);
        var retrievedMstEntries = pdsDb.GetMstEntriesForNodeObjectId((Guid)mstNodeToInsert.NodeObjectId);

        // Assert
        Assert.NotNull(retrievedMstNode);
        Assert.Equal(mstNodeToInsert.Cid?.Base32, retrievedMstNode!.Cid?.Base32);
        Assert.Single(retrievedMstEntries);
        Assert.Equal("exampleKey", retrievedMstEntries[0].KeySuffix);
        Assert.Equal(0, retrievedMstEntries[0].PrefixLength);
        Assert.Equal(0, retrievedMstEntries[0].EntryIndex);

        pdsDb.DeleteMstNodeByObjectId((Guid)mstNodeToInsert.NodeObjectId);
}


    [Fact]
    public void MstNode_InsertNodeWithTwoEntries()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;
        var nodeCid = Guid.NewGuid().ToString();

        var mstNodeToInsert = new MstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = CidV1.FromBase32("bafyreifysqafipni5pe6dcxprngm3kybg5cyn5c4szstz6iedysdrcwjdm"),
            LeftMstNodeCid = null,
        };
        var mstEntriesToInsert = new List<MstEntry>
            {
                new MstEntry
                {
                    KeySuffix = "exampleKey1",
                    EntryIndex = 0,
                    PrefixLength = 0,
                    TreeMstNodeCid = null,
                    RecordCid = CidV1.FromBase32("bafyreifysqafipni5pe6dcxprngm3kybg5cyn5c4szstz6iedysdrcwjdm")
                },
                new MstEntry
                {
                    KeySuffix = "ampleKey2",
                    EntryIndex = 1,
                    PrefixLength = 2,
                    TreeMstNodeCid = CidV1.FromBase32("bafyreifjef7rncdlfq347oislx3qiss2gt5jydzquzpjpwye6tsdf4joom"),
                    RecordCid = CidV1.FromBase32("bafyreiagh3ukdhtq2onx3pz2quesxvq5a4ucaqywvtqyjabqpkmibre7p4")
                }
            };

        // Act
        pdsDb!.InsertMstNode(mstNodeToInsert);
        pdsDb!.InsertMstEntries((Guid)mstNodeToInsert.NodeObjectId, mstEntriesToInsert);

        var retrievedMstNode = pdsDb.GetMstNodeByObjectId((Guid)mstNodeToInsert.NodeObjectId);
        var retrievedMstEntries = pdsDb.GetMstEntriesForNodeObjectId((Guid)mstNodeToInsert.NodeObjectId);

        // Assert
        Assert.NotNull(retrievedMstNode);
        Assert.Equal(mstNodeToInsert.Cid?.Base32, retrievedMstNode!.Cid?.Base32);
        Assert.Equal(2, retrievedMstEntries.Count);
        Assert.Equal("exampleKey1", retrievedMstEntries[0].KeySuffix);
        Assert.Equal("ampleKey2", retrievedMstEntries[1].KeySuffix);
        Assert.Equal(2, retrievedMstEntries[1].PrefixLength);
        Assert.Equal(0, retrievedMstEntries[0].PrefixLength);
        Assert.Equal(0, retrievedMstEntries[0].EntryIndex);
        Assert.Equal(1, retrievedMstEntries[1].EntryIndex);

        pdsDb.DeleteMstNodeByObjectId((Guid)mstNodeToInsert.NodeObjectId);

    }

    #endregion





    #region REPORECORD

    [Fact]
    public void RepoRecord_InsertAndRetrieve()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var repoRecordToInsert = RepoRecord.FromDagCborObject(
            CidV1.FromBase32("bafyreifjef7rncdlfq347oislx3qiss2gt5jydzquzpjpwye6tsdf4joom"),
            new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data" } }
                }
            }
        );

        // Act
        pdsDb!.InsertRepoRecord(repoRecordToInsert);

        var retrievedRepoRecord = pdsDb.GetRepoRecord(repoRecordToInsert.Cid);

        // Assert
        Assert.NotNull(retrievedRepoRecord);
        Assert.Equal(repoRecordToInsert.Cid?.Base32, retrievedRepoRecord!.Cid?.Base32);
        Assert.Equal(repoRecordToInsert.DataBlock?.ToString(), retrievedRepoRecord.DataBlock?.ToString());

        pdsDb.DeleteRepoRecord(repoRecordToInsert.Cid);
    }

    [Fact]
    public void RepoRecord_InsertAndDelete()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;

        var repoRecordToInsert = RepoRecord.FromDagCborObject(
            CidV1.FromBase32("bafyreifjef7rncdlfq347oislx3qiss2gt5jydzquzpjpwye6tsdf4joom"),
            new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data" } }
                }
            }
        );

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

        var repoRecord1 = RepoRecord.FromDagCborObject(
            CidV1.FromBase32("bafyreifjef7rncdlfq347oislx3qiss2gt5jydzquzpjpwye6tsdf4joom"),
            new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data" } }
                }
            }
        );
        var repoRecord2 = RepoRecord.FromDagCborObject(
            CidV1.FromBase32("bafyreifysqafipni5pe6dcxprngm3kybg5cyn5c4szstz6iedysdrcwjdm"),
            new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data" } }
                }
            }
        );

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
