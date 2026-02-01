
namespace dnproto.tests.pds;

using System.Text.Json.Nodes;
using dnproto.fs;
using dnproto.log;
using dnproto.pds;
using dnproto.repo;
using dnproto.uri;
using Xunit.v3;

public class UserRepoTestsFixture : IDisposable
{
    public Logger Logger { get; set; } = new Logger();
    public LocalFileSystem Lfs { get; set; }

    public PdsDb PdsDb { get; set; }
    
    public string DataDir { get; set; }

    public UserRepoTestsFixture()
    {
        //
        // Create temp dir
        //
        Logger.AddDestination(new ConsoleLogDestination());
        string tempDir = Path.Combine(Path.GetTempPath(), "userrepo-tests-data-dir");
        Logger.LogInfo($"Using temp dir for tests: {tempDir}");
        DataDir = tempDir;

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


        //
        // Install repo
        //
        Installer.InstallRepo(Lfs, Logger, TestCommitSigner);



    }

    public static Func<byte[], byte[]> TestCommitSigner = (data) =>
    {
        // return hash of bytes
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        return sha256.ComputeHash(data);
    };

    public void Dispose()
    {
    }
}



public class UserRepoTests : IClassFixture<UserRepoTestsFixture>
{
    private readonly UserRepoTestsFixture _fixture;
    private readonly ITestOutputHelper _output;

    public UserRepoTests(UserRepoTestsFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    
    [Fact]
    public void CreateRecord_ReturnValue()
    {
        Assert.NotNull(_fixture.PdsDb);

        //
        // Start with fresh repo
        //
        Installer.InstallRepo(_fixture.Lfs, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner);
        var userRepo = UserRepo.ConnectUserRepo(_fixture.Lfs, _fixture.Logger, _fixture.PdsDb, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");

        //
        // Create record
        //
        JsonNode post1Node = new JsonObject
        {
            ["text"] = "Hello, world!",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        UserRepo.ApplyWritesResult result = userRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = RecordKey.GenerateTid(),
                Record = DagCborObject.FromJsonString(post1Node.ToJsonString())
            }
        }, null, null).First();


        //
        // Assert
        //
        Assert.NotNull(result.Uri);
        Assert.NotNull(result.Cid);
        Assert.Equal("valid", result.ValidationStatus);
        _output.WriteLine($"Created record URI: {result.Uri}");
    }

    
    [Fact]
    public void CreateRecord_GetRecord()
    {
        Assert.NotNull(_fixture.PdsDb);

        //
        // Start with fresh repo
        //
        Installer.InstallRepo(_fixture.Lfs, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner);
        var userRepo = UserRepo.ConnectUserRepo(_fixture.Lfs, _fixture.Logger, _fixture.PdsDb, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");

        //
        // Create record
        //
        JsonNode post1Node = new JsonObject
        {
            ["text"] = "Hello, world!",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        UserRepo.ApplyWritesResult result = userRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = RecordKey.GenerateTid(),
                Record = DagCborObject.FromJsonString(post1Node.ToJsonString())
            }
        }, null, null).First();

        //
        // Get record
        //
        AtUri? atUri = AtUri.FromAtUri(result.Uri);
        Assert.NotNull(atUri);
        Assert.NotNull(atUri?.Collection);
        Assert.NotNull(atUri?.Rkey);

        RepoRecord? fetchedRecord = userRepo.GetRecord(atUri!.Collection!, atUri.Rkey!);

        //
        // Assert
        //
        Assert.NotNull(fetchedRecord);
        Assert.Equal(result.Cid, fetchedRecord!.Cid);
        _output.WriteLine($"Fetched record URI: {result.Uri}");

    }

    [Fact]
    public void CreateRecord_DeleteRecord_GetRecord()
    {
        Assert.NotNull(_fixture.PdsDb);

        //
        // Start with fresh repo
        //
        Installer.InstallRepo(_fixture.Lfs, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner);
        var userRepo = UserRepo.ConnectUserRepo(_fixture.Lfs, _fixture.Logger, _fixture.PdsDb, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");

        //
        // Create record
        //
        JsonNode post1Node = new JsonObject
        {
            ["text"] = "Hello, world!",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        UserRepo.ApplyWritesResult result = userRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = RecordKey.GenerateTid(),
                Record = DagCborObject.FromJsonString(post1Node.ToJsonString())
            }
        }, null, null).First();

        //
        // Delete record
        //
        AtUri? atUriToDelete = AtUri.FromAtUri(result.Uri);
        Assert.NotNull(atUriToDelete);
        userRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Delete,
                Collection = "app.bsky.feed.post",
                Rkey = atUriToDelete!.Rkey!
            }
        }, null, null).First();


        //
        // Get record
        //
        Assert.Throws<Exception>(() => userRepo.GetRecord(atUriToDelete!.Collection!, atUriToDelete.Rkey!));

        //
        // Assert
        //
        Assert.False(userRepo.RecordExists(atUriToDelete!.Collection!, atUriToDelete.Rkey!));

    }

    [Fact]
    public void PutRecord()
    {
        Assert.NotNull(_fixture.PdsDb);

        //
        // Start with fresh repo
        //
        Installer.InstallRepo(_fixture.Lfs, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner);
        var userRepo = UserRepo.ConnectUserRepo(_fixture.Lfs, _fixture.Logger, _fixture.PdsDb, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");

        //
        // Create record
        //
        JsonNode post1Node = new JsonObject
        {
            ["text"] = "record was created",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        UserRepo.ApplyWritesResult result = userRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = RecordKey.GenerateTid(),
                Record = DagCborObject.FromJsonString(post1Node.ToJsonString())
            }
        }, null, null).First();
        AtUri? atUri = AtUri.FromAtUri(result.Uri);

        //
        // Get record
        //
        RepoRecord? fetchedRecord = userRepo.GetRecord(atUri!.Collection!, atUri.Rkey!);
        Assert.Equal("record was created", fetchedRecord!.DataBlock.SelectString(["text"]));


        //
        // Put new record
        //
        JsonNode post2Node = new JsonObject
        {
            ["text"] = "record was updated",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        UserRepo.ApplyWritesResult result2 = userRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Update,
                Collection = "app.bsky.feed.post",
                Rkey = atUri!.Rkey!,
                Record = DagCborObject.FromJsonString(post2Node.ToJsonString())
            }
        }, null, null).First();

        //
        // Get record
        //
        RepoRecord? fetchedRecord2 = userRepo.GetRecord(atUri!.Collection!, atUri.Rkey!);
        Assert.Equal("record was updated", fetchedRecord2!.DataBlock.SelectString(["text"]));



    }


    [Fact]
    public void TwoCreates()
    {
        Assert.NotNull(_fixture.PdsDb);


        //
        // Start with fresh repo
        //
        Installer.InstallRepo(_fixture.Lfs, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner);
        var userRepo = UserRepo.ConnectUserRepo(_fixture.Lfs, _fixture.Logger, _fixture.PdsDb, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");

        //
        // Create two records
        //
        JsonNode post1Node = new JsonObject
        {
            ["text"] = "record was created",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        JsonNode post2Node = new JsonObject
        {
            ["text"] = "record was also created",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        List<UserRepo.ApplyWritesResult> results = userRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = RecordKey.GenerateTid(),
                Record = DagCborObject.FromJsonString(post1Node.ToJsonString())
            },
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = RecordKey.GenerateTid(),
                Record = DagCborObject.FromJsonString(post2Node.ToJsonString())
            }
        }, null, null);

        AtUri? atUri1 = AtUri.FromAtUri(results[0].Uri);
        AtUri? atUri2 = AtUri.FromAtUri(results[1].Uri);

        //
        // Get records
        //
        RepoRecord? fetchedRecord1 = userRepo.GetRecord(atUri1!.Collection!, atUri1.Rkey!);
        Assert.Equal("record was created", fetchedRecord1!.DataBlock.SelectString(["text"]));

        RepoRecord? fetchedRecord2 = userRepo.GetRecord(atUri2!.Collection!, atUri2.Rkey!);
        Assert.Equal("record was also created", fetchedRecord2!.DataBlock.SelectString(["text"]));

    }


    [Fact]
    public void TwoCreatesOneDelete()
    {
        Assert.NotNull(_fixture.PdsDb);


        //
        // Start with fresh repo
        //
        Installer.InstallRepo(_fixture.Lfs, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner);
        var userRepo = UserRepo.ConnectUserRepo(_fixture.Lfs, _fixture.Logger, _fixture.PdsDb, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");

        //
        // Create two records
        //
        string post1nodeRkey = RecordKey.GenerateTid();
        string post2nodeRkey = RecordKey.GenerateTid();

        JsonNode post1Node = new JsonObject
        {
            ["text"] = "record was created",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        JsonNode post2Node = new JsonObject
        {
            ["text"] = "record was also created",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };


        List<UserRepo.ApplyWritesResult> results = userRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = post1nodeRkey,
                Record = DagCborObject.FromJsonString(post1Node.ToJsonString())
            },
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = post2nodeRkey,
                Record = DagCborObject.FromJsonString(post2Node.ToJsonString())
            },
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Delete,
                Collection = "app.bsky.feed.post",
                Rkey = post2nodeRkey
            }
        }, null, null);


        //
        // Assert
        //
        Assert.True(userRepo.RecordExists("app.bsky.feed.post", post1nodeRkey));
        Assert.False(userRepo.RecordExists("app.bsky.feed.post", post2nodeRkey));
    }



    [Fact]
    public void TwoCreatesOneUpdate()
    {
        Assert.NotNull(_fixture.PdsDb);


        //
        // Start with fresh repo
        //
        Installer.InstallRepo(_fixture.Lfs, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner);
        var userRepo = UserRepo.ConnectUserRepo(_fixture.Lfs, _fixture.Logger, _fixture.PdsDb, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");

        //
        // Create two records
        //
        string post1nodeRkey = RecordKey.GenerateTid();
        string post2nodeRkey = RecordKey.GenerateTid();

        JsonNode post1Node = new JsonObject
        {
            ["text"] = "record was created",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        JsonNode post2Node = new JsonObject
        {
            ["text"] = "record was also created",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        JsonNode post2NodeUpdated = new JsonObject
        {
            ["text"] = "record was also updated",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        List<UserRepo.ApplyWritesResult> results = userRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = post1nodeRkey,
                Record = DagCborObject.FromJsonString(post1Node.ToJsonString())
            },
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = post2nodeRkey,
                Record = DagCborObject.FromJsonString(post2Node.ToJsonString())
            },
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Update,
                Collection = "app.bsky.feed.post",
                Rkey = post2nodeRkey,
                Record = DagCborObject.FromJsonString(post2NodeUpdated.ToJsonString())
            }
        }, null, null);


        //
        // Assert
        //
        Assert.True(userRepo.RecordExists("app.bsky.feed.post", post1nodeRkey));
        Assert.True(userRepo.RecordExists("app.bsky.feed.post", post2nodeRkey));

        var post2Record = userRepo.GetRecord("app.bsky.feed.post", post2nodeRkey);
        Assert.NotNull(post2Record);
        Assert.Equal("record was also updated", post2Record.DataBlock.SelectString(["text"]));
    }

    

    [Fact]
    public void FirehoseEvent_TwoCreatesOneUpdate()
    {
        Assert.NotNull(_fixture.PdsDb);


        //
        // Start with fresh repo
        //
        Installer.InstallRepo(_fixture.Lfs, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner);
        var userRepo = UserRepo.ConnectUserRepo(_fixture.Lfs, _fixture.Logger, _fixture.PdsDb, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");
        long mostRecentlyUsedSequenceNumber = _fixture.PdsDb.GetMostRecentlyUsedSequenceNumber();

        //
        // Create two records
        //
        string post1nodeRkey = RecordKey.GenerateTid();
        string post2nodeRkey = RecordKey.GenerateTid();

        JsonNode post1Node = new JsonObject
        {
            ["text"] = "record was created",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        JsonNode post2Node = new JsonObject
        {
            ["text"] = "record was also created",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        JsonNode post2NodeUpdated = new JsonObject
        {
            ["text"] = "record was also updated",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        List<UserRepo.ApplyWritesResult> results = userRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = post1nodeRkey,
                Record = DagCborObject.FromJsonString(post1Node.ToJsonString())
            },
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = post2nodeRkey,
                Record = DagCborObject.FromJsonString(post2Node.ToJsonString())
            },
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Update,
                Collection = "app.bsky.feed.post",
                Rkey = post2nodeRkey,
                Record = DagCborObject.FromJsonString(post2NodeUpdated.ToJsonString())
            }
        }, null, null);


        //
        // Assert the records
        //
        Assert.True(userRepo.RecordExists("app.bsky.feed.post", post1nodeRkey));
        Assert.True(userRepo.RecordExists("app.bsky.feed.post", post2nodeRkey));

        var post2Record = userRepo.GetRecord("app.bsky.feed.post", post2nodeRkey);
        Assert.NotNull(post2Record);
        Assert.Equal("record was also updated", post2Record.DataBlock.SelectString(["text"]));


        //
        // Get the firehose event
        //
        List<FirehoseEvent> firehoseEvents = _fixture.PdsDb.GetFirehoseEventsForSubscribeRepos(mostRecentlyUsedSequenceNumber);
        Assert.Single(firehoseEvents);
    }


    
    [Fact]
    public void FirehoseEvent_TwoCreatesOneUpdate_CheckProperties()
    {
        Assert.NotNull(_fixture.PdsDb);


        //
        // Start with fresh repo
        //
        string userDid = "did:example:testuser";
        Installer.InstallRepo(_fixture.Lfs, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner);
        var userRepo = UserRepo.ConnectUserRepo(_fixture.Lfs, _fixture.Logger, _fixture.PdsDb, UserRepoTestsFixture.TestCommitSigner, userDid);
        long mostRecentlyUsedSequenceNumber = _fixture.PdsDb.GetMostRecentlyUsedSequenceNumber();

        //
        // Create two records
        //
        string post1nodeRkey = RecordKey.GenerateTid();
        string post2nodeRkey = RecordKey.GenerateTid();

        JsonNode post1Node = new JsonObject
        {
            ["text"] = "record was created",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        JsonNode post2Node = new JsonObject
        {
            ["text"] = "record was also created",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        JsonNode post2NodeUpdated = new JsonObject
        {
            ["text"] = "record was also updated",
            ["createdAt"] = DateTime.UtcNow.ToString("o")
        };

        List<UserRepo.ApplyWritesResult> results = userRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = post1nodeRkey,
                Record = DagCborObject.FromJsonString(post1Node.ToJsonString())
            },
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.feed.post",
                Rkey = post2nodeRkey,
                Record = DagCborObject.FromJsonString(post2Node.ToJsonString())
            },
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Update,
                Collection = "app.bsky.feed.post",
                Rkey = post2nodeRkey,
                Record = DagCborObject.FromJsonString(post2NodeUpdated.ToJsonString())
            }
        }, null, null);


        //
        // Assert the records
        //
        Assert.True(userRepo.RecordExists("app.bsky.feed.post", post1nodeRkey));
        Assert.True(userRepo.RecordExists("app.bsky.feed.post", post2nodeRkey));

        var post2Record = userRepo.GetRecord("app.bsky.feed.post", post2nodeRkey);
        Assert.NotNull(post2Record);
        Assert.Equal("record was also updated", post2Record.DataBlock.SelectString(["text"]));


        //
        // Get the firehose event
        //
        List<FirehoseEvent> firehoseEvents = _fixture.PdsDb.GetFirehoseEventsForSubscribeRepos(mostRecentlyUsedSequenceNumber);
        Assert.Single(firehoseEvents);

        DagCborObject dagCborHeader = firehoseEvents[0].Header_DagCborObject;
        DagCborObject dagCborBody = firehoseEvents[0].Body_DagCborObject;

        Assert.Equal("#commit", dagCborHeader.SelectString(["t"]));
        Assert.Equal(1, dagCborHeader.SelectInt(["op"]));
        Assert.Equal((mostRecentlyUsedSequenceNumber+1), dagCborBody.SelectLong(["seq"]));
        Assert.Equal(_fixture.PdsDb.GetRepoCommit()!.Cid!.Base32, dagCborBody.SelectString(["commit"]));
        Assert.Equal(userDid, dagCborBody.SelectString(["repo"]));
    }

}
