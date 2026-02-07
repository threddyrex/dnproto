
namespace dnproto.tests.pds;

using System.Reflection;
using dnproto.fs;
using dnproto.log;
using dnproto.pds;
using dnproto.repo;

public class PdsDbTestsFixture : IDisposable
{
    Logger Logger { get; set; } = new Logger();
    public LocalFileSystem? Lfs { get; set; }

    public PdsDb PdsDb { get; set; }

    public PdsDbTestsFixture()
    {
        //
        // Set up temp directory
        //
        Logger.AddDestination(new ConsoleLogDestination());
        string tempDir = Path.Combine(Path.GetTempPath(), "dnproto-tests-data-dir");
        Logger.LogInfo($"Using temp dir for tests: {tempDir}");

        if(!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);            
        }

        //
        // Initialize LFS
        //
        Lfs = LocalFileSystem.Initialize(tempDir, Logger);


        //
        // Install db
        //
        string pdsDbFile = Path.Combine(tempDir, "pds", "pds.db");
        Logger.LogInfo($"PDS database file path: {pdsDbFile}");
        if (File.Exists(pdsDbFile))
        {
            File.Delete(pdsDbFile);
        }

        Installer.InstallDb(Lfs, Logger, deleteExistingDb: false);
        PdsDb = PdsDb.ConnectPdsDb(Lfs, Logger);


        //
        // Install config
        //
        Installer.InstallConfig(Lfs, Logger, "example.com", "availabledomain", "userhandle", "userdid", "useremail");
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
        string pdsDbFilePath = Path.Combine(lfs!.GetDataDir(), "pds", "pds.db");
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
        pdsDb.DeleteConfig();

        var configToInsert = new Config
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
            UserPrivateKeyMultibase = "zPrivateKeyMultibaseExample",
            UserIsActive = true,
            OauthIsEnabled = true,
            PdsCrawlers = new string[] { "bsky.network", "example.crawler" },
            RequestCrawlIsEnabled = true,
            LogRetentionDays = 7,
            AdminInterfaceIsEnabled = true,
            PasskeysEnabled = true
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
        Assert.Equal(configToInsert.OauthIsEnabled, retrievedConfig.OauthIsEnabled);
        Assert.Equal(configToInsert.UserEmail, retrievedConfig.UserEmail);
        Assert.Equal(configToInsert.UserPublicKeyMultibase, retrievedConfig.UserPublicKeyMultibase);
        Assert.Equal(configToInsert.UserPrivateKeyMultibase, retrievedConfig.UserPrivateKeyMultibase);
        Assert.Equal(configToInsert.UserIsActive, retrievedConfig.UserIsActive);
        Assert.Equal(configToInsert.PdsCrawlers, retrievedConfig.PdsCrawlers);
        Assert.Equal(configToInsert.RequestCrawlIsEnabled, retrievedConfig.RequestCrawlIsEnabled);
        Assert.Equal(configToInsert.LogRetentionDays, retrievedConfig.LogRetentionDays);
        Assert.Equal(1, configCount);
        Assert.Equal(configToInsert.AdminInterfaceIsEnabled, retrievedConfig.AdminInterfaceIsEnabled);
        Assert.Equal(configToInsert.PasskeysEnabled, retrievedConfig.PasskeysEnabled);
    }

    [Fact]
    public void Config_SetUserActive()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.SetUserActive(false);
        Assert.False(pdsDb.IsUserActive());
        pdsDb.SetUserActive(true);
        Assert.True(pdsDb.IsUserActive());
    }

    [Fact]
    public void Config_SetOauthEnabled()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.SetOauthEnabled(false);
        Assert.False(pdsDb.GetConfig().OauthIsEnabled);
        pdsDb.SetOauthEnabled(true);
        Assert.True(pdsDb.GetConfig().OauthIsEnabled);
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


        Assert.Throws<Exception>(() =>
        {
            pdsDb.GetRepoHeader();
        });
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
            Did = "did:web:test",
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
            Did = "did:web:test",
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

        Assert.Throws<Exception>(() => 
        {
            var retrievedAfterDelete = pdsDb.GetRepoCommit();
        });
    }

    #endregion




    #region REPORECORD

    [Fact]
    public void RepoRecord_InsertAndRetrieve()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllRepoRecords();

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
        pdsDb!.InsertRepoRecord("collection1", "rkey1", repoRecordToInsert.Cid!, repoRecordToInsert.DataBlock!);

        var retrievedRepoRecord = pdsDb.GetRepoRecord("collection1", "rkey1");

        // Assert
        Assert.NotNull(retrievedRepoRecord);
        Assert.Equal(repoRecordToInsert.Cid?.Base32, retrievedRepoRecord!.Cid?.Base32);
        Assert.Equal(repoRecordToInsert.DataBlock?.ToString(), retrievedRepoRecord.DataBlock?.ToString());


        pdsDb.DeleteRepoRecord("collection1", "rkey1");
    }


    [Fact]
    public void RepoRecord_Exists()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllRepoRecords();

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
        pdsDb!.InsertRepoRecord("collection1", "rkey1", repoRecordToInsert.Cid!, repoRecordToInsert.DataBlock!);

        // Assert
        Assert.True(pdsDb.RecordExists("collection1", "rkey1"));
        Assert.False(pdsDb.RecordExists("collection1", "rkey2"));
    }

    [Fact]
    public void RepoRecord_InsertAndDelete()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllRepoRecords();

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

        string collection = "collection1";
        string rkey = "rkey1";

        // Act
        pdsDb!.InsertRepoRecord(collection, rkey, repoRecordToInsert.Cid!, repoRecordToInsert.DataBlock!);

        var retrievedBeforeDelete = pdsDb.GetRepoRecord(collection, rkey);
        Assert.NotNull(retrievedBeforeDelete);
        Assert.Equal(repoRecordToInsert.Cid?.Base32, retrievedBeforeDelete!.Cid?.Base32);

        pdsDb.DeleteRepoRecord(collection, rkey);

        Assert.Throws<Exception>(() => pdsDb.GetRepoRecord(collection, rkey));
        Assert.False(pdsDb.RecordExists(collection, rkey));
    }

    [Fact]
    public void RepoRecord_DeleteAll()
    {
        // Arrange
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllRepoRecords();

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
        pdsDb!.InsertRepoRecord("collection1", "rkey1", repoRecord1.Cid!, repoRecord1.DataBlock!);
        pdsDb.InsertRepoRecord("collection2", "rkey2", repoRecord2.Cid!, repoRecord2.DataBlock!);

        pdsDb.DeleteAllRepoRecords();

        Assert.Throws<Exception>(() => pdsDb.GetRepoRecord("collection1", "rkey1"));
        Assert.Throws<Exception>(() => pdsDb.GetRepoRecord("collection2", "rkey2"));
        Assert.False(pdsDb.RecordExists("collection1", "rkey1"));
        Assert.False(pdsDb.RecordExists("collection2", "rkey2"));
    }
    #endregion


    #region SEQ

    [Fact]
    public void SequenceNumber()
    {
        var pdsDb = _fixture.PdsDb;

        pdsDb.DeleteSequenceNumber();
        Assert.Equal(0, pdsDb.GetMostRecentlyUsedSequenceNumber());
        Assert.Equal(1, pdsDb.GetNewSequenceNumberForFirehose());
        Assert.Equal(2, pdsDb.GetNewSequenceNumberForFirehose());
        Assert.Equal(3, pdsDb.GetNewSequenceNumberForFirehose());
    }



    #endregion


    #region FIREHOSE

    [Fact]
    public void FirehoseEvent_Insert()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllFirehoseEvents();
        var firehoseEvent = new FirehoseEvent
        {
            SequenceNumber = 1,
            CreatedDate = DateTime.UtcNow.ToString("o"),
            Header_op = 1,
            Header_t = "test header_t",
            Header_DagCborObject = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example header", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data header" } }
                }
            },
            Body_DagCborObject = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example body", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data body" } }
                }
            }
        };

        pdsDb.InsertFirehoseEvent(firehoseEvent);

        var retrievedEvent = pdsDb.GetFirehoseEvent(firehoseEvent.SequenceNumber);
        Assert.NotNull(retrievedEvent);
        Assert.Equal(firehoseEvent.SequenceNumber, retrievedEvent.SequenceNumber);
        Assert.Equal(firehoseEvent.CreatedDate, retrievedEvent.CreatedDate);
        Assert.Equal(firehoseEvent.Header_op, retrievedEvent.Header_op);
        Assert.Equal(firehoseEvent.Header_t, retrievedEvent.Header_t);
        Assert.Equal(firehoseEvent.Header_DagCborObject.ToString(), retrievedEvent.Header_DagCborObject.ToString());
        Assert.Equal(firehoseEvent.Body_DagCborObject.ToString(), retrievedEvent.Body_DagCborObject?.ToString());
    }


    
    [Fact]
    public void FirehoseEvent_Insert2()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllFirehoseEvents();


        var firehoseEvent = new FirehoseEvent
        {
            SequenceNumber = 1,
            CreatedDate = DateTime.UtcNow.ToString("o"),
            Header_op = 1,
            Header_t = "test header_t",
            Header_DagCborObject = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example header", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data header" } }
                }
            },
            Body_DagCborObject = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example body", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data body" } }
                }
            }
        };

        pdsDb.InsertFirehoseEvent(firehoseEvent);



        var firehoseEvent2 = new FirehoseEvent
        {
            SequenceNumber = 2,
            CreatedDate = DateTime.UtcNow.ToString("o"),
            Header_op = 1,
            Header_t = "test header_t",
            Header_DagCborObject = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example header", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data header" } }
                }
            },
            Body_DagCborObject = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example body", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data body" } }
                }
            }
        };

        pdsDb.InsertFirehoseEvent(firehoseEvent2);



        var retrievedEvent = pdsDb.GetFirehoseEvent(firehoseEvent2.SequenceNumber);
        Assert.NotNull(retrievedEvent);
        Assert.Equal(firehoseEvent2.SequenceNumber, retrievedEvent.SequenceNumber);
        Assert.Equal(firehoseEvent2.CreatedDate, retrievedEvent.CreatedDate);
        Assert.Equal(firehoseEvent2.Header_op, retrievedEvent.Header_op);
        Assert.Equal(firehoseEvent2.Header_t, retrievedEvent.Header_t);
        Assert.Equal(firehoseEvent2.Header_DagCborObject.ToString(), retrievedEvent.Header_DagCborObject.ToString());
        Assert.Equal(firehoseEvent2.Body_DagCborObject.ToString(), retrievedEvent.Body_DagCborObject?.ToString());
    }



    [Fact]
    public void FirehoseEvent_DoesntExist()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllFirehoseEvents();
        var firehoseEvent = new FirehoseEvent
        {
            SequenceNumber = 1,
            CreatedDate = DateTime.UtcNow.ToString("o"),
            Header_op = 1,
            Header_t = "test header_t",
            Header_DagCborObject = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example header", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data header" } }
                }
            },
            Body_DagCborObject = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 1 },
                Value = new Dictionary<string, DagCborObject>
                {
                    { "example body", new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 7 }, Value = "data body" } }
                }
            }
        };

        pdsDb.InsertFirehoseEvent(firehoseEvent);

        Assert.Throws<Exception>(() => pdsDb.GetFirehoseEvent(3));
    }





    #endregion


    #region LogLevel

    [Fact]
    public void LogLevel_SetLevel()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteLogLevel();

        Assert.Equal(0, pdsDb.GetLogLevelCount());
        Assert.Equal("info", pdsDb.GetLogLevel());

        pdsDb.SetLogLevel("trace");
        Assert.Equal(1, pdsDb.GetLogLevelCount());
        Assert.Equal("trace", pdsDb.GetLogLevel());

        pdsDb.SetLogLevel("trace");
        Assert.Equal(1, pdsDb.GetLogLevelCount());
        Assert.Equal("trace", pdsDb.GetLogLevel());

        pdsDb.SetLogLevel("debug");
        Assert.Equal(1, pdsDb.GetLogLevelCount());
        Assert.Equal("debug", pdsDb.GetLogLevel());

        pdsDb.SetLogLevel("error");
        Assert.Equal(1, pdsDb.GetLogLevelCount());
        Assert.Equal("error", pdsDb.GetLogLevel());
    }
    #endregion


    #region OAUTHREQ

    [Fact]
    public void OauthRequest_InsertAndGet()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllOauthRequests();

        string requestUri = Guid.NewGuid().ToString();
        var oauthRequest = new OauthRequest
        {
            RequestUri = requestUri,
            ExpiresDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow.AddMinutes(5)),
            Dpop = "dpop",
            Body = "body"
        };

        pdsDb.InsertOauthRequest(oauthRequest);

        var retrievedRequest = pdsDb.GetOauthRequest(oauthRequest.RequestUri);
        Assert.NotNull(retrievedRequest);
        Assert.Equal(oauthRequest.RequestUri, retrievedRequest.RequestUri);
        Assert.Equal(oauthRequest.ExpiresDate, retrievedRequest.ExpiresDate);
        Assert.Equal(oauthRequest.Dpop, retrievedRequest.Dpop);
        Assert.Equal(oauthRequest.Body, retrievedRequest.Body);
    }


    [Fact]
    public void OauthRequest_InsertGetExpired()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllOauthRequests();

        string requestUri = Guid.NewGuid().ToString();
        var oauthRequest = new OauthRequest
        {
            RequestUri = requestUri,
            ExpiresDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow.AddMinutes(-1)),
            Dpop = "dpop",
            Body = "body"
        };

        pdsDb.InsertOauthRequest(oauthRequest);

        Assert.Throws<Exception>(() => pdsDb.GetOauthRequest(oauthRequest.RequestUri));
    }


    [Fact]
    public void OauthRequest_InsertAndGetByAuthorizationCode()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllOauthRequests();

        string requestUri = Guid.NewGuid().ToString();
        var oauthRequest = new OauthRequest
        {
            RequestUri = requestUri,
            ExpiresDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow.AddMinutes(5)),
            Dpop = "dpop",
            Body = "body",
            AuthorizationCode = "authcode"
        };

        pdsDb.InsertOauthRequest(oauthRequest);

        var retrievedRequest = pdsDb.GetOauthRequestByAuthorizationCode(oauthRequest.AuthorizationCode);
        Assert.NotNull(retrievedRequest);
        Assert.Equal(oauthRequest.RequestUri, retrievedRequest.RequestUri);
        Assert.Equal(oauthRequest.ExpiresDate, retrievedRequest.ExpiresDate);
        Assert.Equal(oauthRequest.Dpop, retrievedRequest.Dpop);
        Assert.Equal(oauthRequest.Body, retrievedRequest.Body);
        Assert.Equal(oauthRequest.AuthorizationCode, retrievedRequest.AuthorizationCode);
    }


    [Fact]
    public void OauthRequest_Update()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllOauthRequests();

        string requestUri = Guid.NewGuid().ToString();
        var oauthRequest = new OauthRequest
        {
            RequestUri = requestUri,
            ExpiresDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow.AddMinutes(5)),
            Dpop = "dpop",
            Body = "body",
            AuthorizationCode = null
        };

        pdsDb.InsertOauthRequest(oauthRequest);
        oauthRequest.AuthorizationCode = "authcode";
        pdsDb.UpdateOauthRequest(oauthRequest);

        var retrievedRequest = pdsDb.GetOauthRequest(oauthRequest.RequestUri);
        Assert.NotNull(retrievedRequest);
        Assert.Equal(oauthRequest.RequestUri, retrievedRequest.RequestUri);
        Assert.Equal(oauthRequest.ExpiresDate, retrievedRequest.ExpiresDate);
        Assert.Equal(oauthRequest.Dpop, retrievedRequest.Dpop);
        Assert.Equal(oauthRequest.Body, retrievedRequest.Body);
        Assert.Equal(oauthRequest.AuthorizationCode, retrievedRequest.AuthorizationCode);
    }


    #endregion


    #region OAUTHSESS

    [Fact]
    public void OauthSession_InsertGet()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllOauthSessions();

        string sessionId = Guid.NewGuid().ToString();
        var oauthSession = new OauthSession
        {
            SessionId = sessionId,
            ClientId = "clientId",
            Scope = "scope",
            DpopJwkThumbprint = "dpopJwkThumbprint",
            RefreshToken = "refreshToken",
            RefreshTokenExpiresDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow.AddMinutes(5)),
            CreatedDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow),
            IpAddress = "ipaddr"
        };

        pdsDb.InsertOauthSession(oauthSession);

        var retrievedSession = pdsDb.GetOauthSessionBySessionId(oauthSession.SessionId);
        Assert.NotNull(retrievedSession);
        Assert.Equal(oauthSession.SessionId, retrievedSession.SessionId);
        Assert.Equal(oauthSession.ClientId, retrievedSession.ClientId);
        Assert.Equal(oauthSession.Scope, retrievedSession.Scope);
        Assert.Equal(oauthSession.DpopJwkThumbprint, retrievedSession.DpopJwkThumbprint);
        Assert.Equal(oauthSession.RefreshToken, retrievedSession.RefreshToken);
        Assert.Equal(oauthSession.RefreshTokenExpiresDate, retrievedSession.RefreshTokenExpiresDate);
        Assert.Equal(oauthSession.CreatedDate, retrievedSession.CreatedDate);
        Assert.Equal(oauthSession.IpAddress, retrievedSession.IpAddress);
    }

    [Fact]
    public void OauthSession_DeleteOld()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllOauthSessions();

        string sessionId = Guid.NewGuid().ToString();
        var oauthSession = new OauthSession
        {
            SessionId = sessionId,
            ClientId = "clientId",
            Scope = "scope",
            DpopJwkThumbprint = "dpopJwkThumbprint",
            RefreshToken = "refreshToken",
            RefreshTokenExpiresDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow.AddMinutes(-5)),
            CreatedDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow),
            IpAddress = "ipaddr"
        };

        pdsDb.InsertOauthSession(oauthSession);
        pdsDb.DeleteOldOauthSessions();

        Assert.Throws<Exception>(() => pdsDb.GetOauthSessionBySessionId(oauthSession.SessionId));
    }

    #endregion


    #region LEGACY


    [Fact]
    public void LegacySession_Create()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllLegacySessions();


        var session = new LegacySession()
        {
            AccessJwt = "accessjwt",
            RefreshJwt = "refreshjwt",
            CreatedDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow),
            IpAddress = "ipaddr",
            UserAgent = "useragent"
        };

        pdsDb.CreateLegacySession(session);

        Assert.True(pdsDb.LegacySessionExistsForAccessJwt(session.AccessJwt));
        Assert.True(pdsDb.LegacySessionExistsForRefreshJwt(session.RefreshJwt));
        Assert.False(pdsDb.LegacySessionExistsForAccessJwt("nonexistent"));
        Assert.False(pdsDb.LegacySessionExistsForRefreshJwt("nonexistent"));
    }

    [Fact]
    public void LegacySession_DeleteForRefreshJwt()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllLegacySessions();

        var session = new LegacySession()
        {
            AccessJwt = "accessjwt",
            RefreshJwt = "refreshjwt",
            CreatedDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow),
            IpAddress = "ipaddr",
            UserAgent = "useragent"
        };
        pdsDb.CreateLegacySession(session);

        Assert.True(pdsDb.LegacySessionExistsForAccessJwt(session.AccessJwt));
        Assert.True(pdsDb.LegacySessionExistsForRefreshJwt(session.RefreshJwt));

        pdsDb.DeleteLegacySessionForRefreshJwt(session.RefreshJwt);

        Assert.False(pdsDb.LegacySessionExistsForAccessJwt(session.AccessJwt));
        Assert.False(pdsDb.LegacySessionExistsForRefreshJwt(session.RefreshJwt));
    }

    #endregion


    #region ADMINSESS

    [Fact]
    public void AdminSession_CreateAndDelete()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllAdminSessions();

        string sessionId = Guid.NewGuid().ToString();

        var adminSession = new AdminSession
        {
            SessionId = sessionId,
            IpAddress = "ipaddr",
            CreatedDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow),
            UserAgent = "useragent",
            AuthType = "authType"
        };

        pdsDb.InsertAdminSession(adminSession);

        AdminSession? retrievedSession = pdsDb.GetValidAdminSession(sessionId, "ipaddr");

        Assert.True(retrievedSession != null);

        Assert.Equal(adminSession.SessionId, retrievedSession!.SessionId);
        Assert.Equal(adminSession.IpAddress, retrievedSession.IpAddress);
        Assert.Equal(adminSession.CreatedDate, retrievedSession.CreatedDate);
        Assert.Equal(adminSession.UserAgent, retrievedSession.UserAgent);
        Assert.Equal(adminSession.AuthType, retrievedSession.AuthType);
        pdsDb.DeleteAllAdminSessions();


        Assert.Null(pdsDb.GetValidAdminSession(sessionId, "ipaddr"));
    }


    [Fact]
    public void AdminSession_CreateAndGetInvalid()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllAdminSessions();

        string sessionId = Guid.NewGuid().ToString();

        var adminSession = new AdminSession
        {
            SessionId = sessionId,
            IpAddress = "ipaddr",
            CreatedDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow.AddHours(-2)),
            UserAgent = "useragent",
            AuthType = "authType"
        };

        pdsDb.InsertAdminSession(adminSession);

        AdminSession? retrievedSession = pdsDb.GetValidAdminSession(sessionId, "ipaddr");

        Assert.Null(retrievedSession);
    }


    #endregion


    #region PASSKEY

    [Fact]
    public void Passkey_InsertAndRetrieve()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllPasskeys();

        var passkey = new Passkey
        {
            Name = Guid.NewGuid().ToString(),
            CredentialId = "zCredentialIdExample",
            PublicKey = "zPublicKeyExample",
            CreatedDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow)
        };

        pdsDb.InsertPasskey(passkey);

        var retrievedPasskey = pdsDb.GetPasskeyByCredentialId(passkey.CredentialId);
        Assert.NotNull(retrievedPasskey);
        Assert.Equal(passkey.Name, retrievedPasskey!.Name);
        Assert.Equal(passkey.CredentialId, retrievedPasskey.CredentialId);
        Assert.Equal(passkey.PublicKey, retrievedPasskey.PublicKey);
        Assert.Equal(passkey.CreatedDate, retrievedPasskey.CreatedDate);
    }
    

    #endregion



    #region PKEYCHALL

    [Fact]
    public void PasskeyChallenge_InsertAndRetrieve()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllPasskeyChallenges();

        var passkeyChallenge = new PasskeyChallenge
        {
            CreatedDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow),
            Challenge = "challenge_example"
        };

        pdsDb.InsertPasskeyChallenge(passkeyChallenge);

        var retrievedPasskeyChallenge = pdsDb.GetPasskeyChallenge(passkeyChallenge.Challenge);
        Assert.NotNull(retrievedPasskeyChallenge);
        Assert.Equal(passkeyChallenge.Challenge, retrievedPasskeyChallenge!.Challenge);
        Assert.Equal(passkeyChallenge.CreatedDate, retrievedPasskeyChallenge.CreatedDate);
    }
    

    #endregion


    #region STATS

    [Fact]
    public void Stats_InsertAndRetrieve()
    {
        var pdsDb = _fixture.PdsDb;
        pdsDb.DeleteAllStatistics();

        pdsDb.IncrementStatistic(new StatisticKey{ Name = "active_users", IpAddress = "userip", UserAgent = "useragent" });
        pdsDb.IncrementStatistic(new StatisticKey{ Name = "active_users", IpAddress = "userip", UserAgent = "useragent" });

        Assert.Equal(2, pdsDb.GetStatisticValue(new StatisticKey{ Name = "active_users", IpAddress = "userip", UserAgent = "useragent" }));
        Assert.True(pdsDb.StatisticExists(new StatisticKey{ Name = "active_users", IpAddress = "userip", UserAgent = "useragent" }));
        pdsDb.IncrementStatistic(new StatisticKey{ Name = "active_users", IpAddress = "userip", UserAgent = "useragent" });
        Assert.Equal(3, pdsDb.GetStatisticValue(new StatisticKey{ Name = "active_users", IpAddress = "userip", UserAgent = "useragent" }));

        var stats = pdsDb.GetAllStatistics();

        Assert.Single(stats);

        Assert.Equal("active_users", stats[0].Name);
        Assert.Equal("userip", stats[0].IpAddress);
        Assert.Equal("useragent", stats[0].UserAgent);
        Assert.Equal(3, stats[0].Value);
        Assert.NotNull(stats[0].LastUpdatedDate);

    }

    #endregion
}
