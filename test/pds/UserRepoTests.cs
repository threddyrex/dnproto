
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
    public LocalFileSystem? Lfs { get; set; }

    public PdsDb? PdsDb { get; set; }

    public UserRepoTestsFixture()
    {
        Logger.AddDestination(new ConsoleLogDestination());
        string tempDir = Path.Combine(Path.GetTempPath(), "userrepo-tests-data-dir");
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
        if(PdsDb is null)
        {
            throw new Exception("Failed to install PDS database for tests.");
        }

        UserRepo.InstallRepo(PdsDb, Logger, TestCommitSigner, "did:example:testuser");
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
        UserRepo.InstallRepo(_fixture.PdsDb, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");
        var userRepo = new UserRepo(_fixture.PdsDb, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");

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
        UserRepo.InstallRepo(_fixture.PdsDb, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");
        var userRepo = new UserRepo(_fixture.PdsDb, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");

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
        UserRepo.InstallRepo(_fixture.PdsDb, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");
        var userRepo = new UserRepo(_fixture.PdsDb, _fixture.Logger, UserRepoTestsFixture.TestCommitSigner, "did:example:testuser");

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
}
