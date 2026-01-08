
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

        (string? uri, 
        RepoRecord? repoRecord, 
        RepoCommit? repoCommit, 
        string? validationStatus)  = userRepo.CreateRecord("app.bsky.feed.post", DagCborObject.FromJsonString(post1Node.ToJsonString())!);


        //
        // Assert
        //
        Assert.NotNull(uri);
        Assert.NotNull(repoRecord);
        Assert.NotNull(repoCommit);
        Assert.Equal("valid", validationStatus);
        _output.WriteLine($"Created record URI: {uri}");
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

        (string? uri, 
        RepoRecord? repoRecord, 
        RepoCommit? repoCommit, 
        string? validationStatus)  = userRepo.CreateRecord("app.bsky.feed.post", DagCborObject.FromJsonString(post1Node.ToJsonString())!);

        //
        // Get record
        //
        AtUri? atUri = AtUri.FromAtUri(uri);
        Assert.NotNull(atUri);
        Assert.NotNull(atUri?.Collection);
        Assert.NotNull(atUri?.Rkey);

        RepoRecord? fetchedRecord = userRepo.GetRecord(atUri!.Collection!, atUri.Rkey!);

        //
        // Assert
        //
        Assert.NotNull(fetchedRecord);
        Assert.Equal(repoRecord!.Cid, fetchedRecord!.Cid);
        _output.WriteLine($"Fetched record URI: {uri}");

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

        (string? uri, 
        RepoRecord? repoRecord, 
        RepoCommit? repoCommit, 
        string? validationStatus)  = userRepo.CreateRecord("app.bsky.feed.post", DagCborObject.FromJsonString(post1Node.ToJsonString())!);

        //
        // Delete record
        //
        AtUri? atUriToDelete = AtUri.FromAtUri(uri);
        userRepo.DeleteRecord("app.bsky.feed.post", atUriToDelete!.Rkey!);


        //
        // Get record
        //
        RepoRecord? fetchedRecord = userRepo.GetRecord(atUriToDelete!.Collection!, atUriToDelete.Rkey!);

        //
        // Assert
        //
        Assert.Null(fetchedRecord);

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

        (string? uri, 
        RepoRecord? repoRecord, 
        RepoCommit? repoCommit, 
        string? validationStatus)  = userRepo.CreateRecord("app.bsky.feed.post", DagCborObject.FromJsonString(post1Node.ToJsonString())!);
        AtUri? atUri = AtUri.FromAtUri(uri);

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

        (string? uri2, 
        RepoRecord? repoRecord2, 
        RepoCommit? repoCommit2, 
        string? validationStatus2)  = userRepo.PutRecord("app.bsky.feed.post", atUri!.Rkey!, DagCborObject.FromJsonString(post2Node.ToJsonString())!);


        //
        // Get record
        //
        RepoRecord? fetchedRecord2 = userRepo.GetRecord(atUri!.Collection!, atUri.Rkey!);
        Assert.Equal("record was updated", fetchedRecord2!.DataBlock.SelectString(["text"]));



    }
}
