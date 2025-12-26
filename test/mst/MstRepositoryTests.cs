namespace dnproto.tests.mst;

using dnproto.mst;
using dnproto.repo;
using System.Linq;

public class MstRepositoryTests
{
    [Fact]
    public void MstRepository_RoundTrip_PreservesData()
    {
        // Arrange - Create a repository with several records
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash; // Simple identity signer for testing

        var originalRepo = MstRepository.CreateForNewUser(did, signer);

        // Add multiple records
        var record1 = CreateTestRecord("First post", "app.bsky.feed.post");
        var record2 = CreateTestRecord("Second post", "app.bsky.feed.post");
        var record3 = CreateTestRecord("Third post", "app.bsky.feed.post");
        var record4 = CreateTestRecord("Test profile", "app.bsky.actor.profile");

        originalRepo.CreateRecord("app.bsky.feed.post/3kj1aaa", record1);
        originalRepo.CreateRecord("app.bsky.feed.post/3kj1bbb", record2);
        originalRepo.CreateRecord("app.bsky.feed.post/3kj1ccc", record3);
        originalRepo.CreateRecord("app.bsky.actor.profile/self", record4);

        // Commit the changes
        var commitEvent = originalRepo.Commit(signer);

        // Act - Write to stream
        using var stream = new MemoryStream();
        MstCarFile.WriteToStream(stream, originalRepo.CurrentCommit!, originalRepo.Mst);

        // Act - Read from stream
        stream.Position = 0;
        var (readCommit, readMst) = MstCarFile.ReadFromStream(stream);
        var loadedRepo = new MstRepository(did, readCommit, readMst);

        // Assert - Compare repositories
        // 1. Check that the same number of records exist
        var originalRecords = originalRepo.ListRecords();
        var loadedRecords = loadedRepo.ListRecords();
        
        Assert.Equal(originalRecords.Count, loadedRecords.Count);
        Assert.Equal(4, loadedRecords.Count);

        // 2. Check that all record keys match
        var sortedOriginal = originalRecords.OrderBy(x => x).ToList();
        var sortedLoaded = loadedRecords.OrderBy(x => x).ToList();
        Assert.Equal(sortedOriginal, sortedLoaded);

        // 3. Check that record data matches
        foreach (var key in originalRecords)
        {
            var originalData = originalRepo.Mst.Get(key);
            var loadedData = loadedRepo.Mst.Get(key);
            
            Assert.NotNull(originalData);
            Assert.NotNull(loadedData);
            Assert.Equal(originalData, loadedData);
        }

        // 4. Check commit properties match
        Assert.Equal(originalRepo.CurrentCommit!.Did, loadedRepo.CurrentCommit!.Did);
        Assert.Equal(originalRepo.CurrentCommit.Rev, loadedRepo.CurrentCommit.Rev);
        Assert.Equal(originalRepo.CurrentCommit.Version, loadedRepo.CurrentCommit.Version);
        
        // 5. Check root CID matches
        var originalRootCid = originalRepo.Mst.Root?.ComputeCid();
        var loadedRootCid = loadedRepo.Mst.Root?.ComputeCid();
        
        Assert.NotNull(originalRootCid);
        Assert.NotNull(loadedRootCid);
        Assert.Equal(originalRootCid.Base32, loadedRootCid.Base32);

        // 6. Check repository statistics match
        var (originalNodeCount, originalRecordCount) = originalRepo.GetStats();
        var (loadedNodeCount, loadedRecordCount) = loadedRepo.GetStats();
        
        // Node counts may differ due to implementation details, but record count should match
        Assert.Equal(originalRecordCount, loadedRecordCount);
        Assert.True(loadedNodeCount > 0, "Loaded repository should have at least one node");
    }

    #region CRUD Operations Tests

    [Fact]
    public void CreateRecord_ThenGetRecord_ReturnsCorrectData()
    {
        // Arrange
        var did = "did:plc:test123";
        var repo = new MstRepository(did);
        var record = CreateTestRecord("Test post", "app.bsky.feed.post");
        var path = "app.bsky.feed.post/3kj1abc";

        // Act
        repo.CreateRecord(path, record);
        var retrieved = repo.GetRecord(path);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(record, retrieved);
    }

    [Fact]
    public void UpdateRecord_ModifiesExistingRecord()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        
        var originalRecord = CreateTestRecord("Original text", "app.bsky.feed.post");
        var updatedRecord = CreateTestRecord("Updated text", "app.bsky.feed.post");
        var path = "app.bsky.feed.post/3kj1abc";

        // Act
        repo.CreateRecord(path, originalRecord);
        repo.Commit(signer);
        
        repo.UpdateRecord(path, updatedRecord);
        var pendingData = repo.GetRecord(path);
        repo.Commit(signer);
        var committedData = repo.GetRecord(path);

        // Assert
        Assert.Equal(updatedRecord, pendingData);
        Assert.Equal(updatedRecord, committedData);
        Assert.NotEqual(originalRecord, committedData);
    }

    [Fact]
    public void DeleteRecord_RemovesRecord()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        
        var record = CreateTestRecord("Test post", "app.bsky.feed.post");
        var path = "app.bsky.feed.post/3kj1abc";

        // Act
        repo.CreateRecord(path, record);
        repo.Commit(signer);
        Assert.NotNull(repo.GetRecord(path));
        
        repo.DeleteRecord(path);
        repo.Commit(signer);
        var deletedRecord = repo.GetRecord(path);

        // Assert
        Assert.Null(deletedRecord);
        Assert.DoesNotContain(path, repo.ListRecords());
    }

    [Fact]
    public void MultipleOperationsOnSameRecord_InOneCommit_AppliesLast()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        
        var record1 = CreateTestRecord("First", "app.bsky.feed.post");
        var record2 = CreateTestRecord("Second", "app.bsky.feed.post");
        var record3 = CreateTestRecord("Third", "app.bsky.feed.post");
        var path = "app.bsky.feed.post/3kj1abc";

        // Act - Create, update, update again in same commit
        repo.CreateRecord(path, record1);
        repo.UpdateRecord(path, record2);
        repo.UpdateRecord(path, record3);
        repo.Commit(signer);
        
        var finalRecord = repo.GetRecord(path);

        // Assert - Should have the last value
        Assert.Equal(record3, finalRecord);
    }

    [Fact]
    public void GetRecord_OnNonExistentPath_ReturnsNull()
    {
        // Arrange
        var did = "did:plc:test123";
        var repo = new MstRepository(did);

        // Act
        var record = repo.GetRecord("app.bsky.feed.post/nonexistent");

        // Assert
        Assert.Null(record);
    }

    #endregion

    #region Pending Changes Tests

    [Fact]
    public void GetRecord_ReturnsPendingChanges_BeforeCommit()
    {
        // Arrange
        var did = "did:plc:test123";
        var repo = new MstRepository(did);
        var record = CreateTestRecord("Pending post", "app.bsky.feed.post");
        var path = "app.bsky.feed.post/3kj1abc";

        // Act
        repo.CreateRecord(path, record);
        var pendingRecord = repo.GetRecord(path);

        // Assert - Should return pending data even before commit
        Assert.NotNull(pendingRecord);
        Assert.Equal(record, pendingRecord);
    }

    [Fact]
    public void Commit_ClearsPendingChanges()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        var record = CreateTestRecord("Test post", "app.bsky.feed.post");

        // Act
        repo.CreateRecord("app.bsky.feed.post/3kj1abc", record);
        repo.Commit(signer);
        
        // Try to commit again without changes
        var exception = Assert.Throws<Exception>(() => repo.Commit(signer));

        // Assert
        Assert.Equal("No pending changes to commit", exception.Message);
    }

    [Fact]
    public void Commit_WithNoPendingChanges_ThrowsException()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = new MstRepository(did);

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => repo.Commit(signer));
        Assert.Equal("No pending changes to commit", exception.Message);
    }

    [Fact]
    public void PendingDelete_ReturnsNull_FromGetRecord()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        var record = CreateTestRecord("Test post", "app.bsky.feed.post");
        var path = "app.bsky.feed.post/3kj1abc";

        // Act
        repo.CreateRecord(path, record);
        repo.Commit(signer);
        
        repo.DeleteRecord(path);
        var pendingRecord = repo.GetRecord(path);

        // Assert - Should return null for pending delete
        Assert.Null(pendingRecord);
    }

    #endregion

    #region Commit & Sequence Number Tests

    [Fact]
    public void SequenceNumber_IncrementsWithEachCommit()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Act & Assert
        Assert.Equal(1, repo.SequenceNumber);
        
        repo.CreateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Post 1", "app.bsky.feed.post"));
        repo.Commit(signer);
        Assert.Equal(2, repo.SequenceNumber);
        
        repo.CreateRecord("app.bsky.feed.post/3kj1bbb", CreateTestRecord("Post 2", "app.bsky.feed.post"));
        repo.Commit(signer);
        Assert.Equal(3, repo.SequenceNumber);
    }

    [Fact]
    public void MultipleSequentialCommits_MaintainCorrectState()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Act - Make multiple commits
        repo.CreateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Post 1", "app.bsky.feed.post"));
        var event1 = repo.Commit(signer);
        
        repo.CreateRecord("app.bsky.feed.post/3kj1bbb", CreateTestRecord("Post 2", "app.bsky.feed.post"));
        var event2 = repo.Commit(signer);
        
        repo.UpdateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Updated Post 1", "app.bsky.feed.post"));
        var event3 = repo.Commit(signer);

        // Assert
        Assert.Equal(2, event1.Seq);
        Assert.Equal(3, event2.Seq);
        Assert.Equal(4, event3.Seq);
        
        // Verify all records still exist
        Assert.Equal(2, repo.ListRecords().Count);
        Assert.NotNull(repo.GetRecord("app.bsky.feed.post/3kj1aaa"));
        Assert.NotNull(repo.GetRecord("app.bsky.feed.post/3kj1bbb"));
    }

    [Fact]
    public void Commit_GeneratesValidFirehoseEvent()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        var record = CreateTestRecord("Test post", "app.bsky.feed.post");
        var path = "app.bsky.feed.post/3kj1abc";

        // Act
        repo.CreateRecord(path, record);
        var firehoseEvent = repo.Commit(signer);

        // Assert
        Assert.NotNull(firehoseEvent);
        Assert.Equal(did, firehoseEvent.Repo);
        Assert.Equal(2, firehoseEvent.Seq);
        Assert.Single(firehoseEvent.Ops);
        Assert.Equal("create", firehoseEvent.Ops[0].Action);
        Assert.Equal(path, firehoseEvent.Ops[0].Path);
        Assert.NotNull(firehoseEvent.Commit);
        Assert.NotNull(firehoseEvent.Rev);
        Assert.False(firehoseEvent.Rebase);
        Assert.False(firehoseEvent.TooBig);
    }

    [Fact]
    public void Commit_RevIsUnique()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Act
        repo.CreateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Post 1", "app.bsky.feed.post"));
        var event1 = repo.Commit(signer);
        
        System.Threading.Thread.Sleep(2); // Ensure time difference
        
        repo.CreateRecord("app.bsky.feed.post/3kj1bbb", CreateTestRecord("Post 2", "app.bsky.feed.post"));
        var event2 = repo.Commit(signer);

        // Assert
        Assert.NotEqual(event1.Rev, event2.Rev);
        Assert.NotNull(event1.Rev);
        Assert.NotNull(event2.Rev);
    }

    [Fact]
    public void Commit_SignsCommitCorrectly()
    {
        // Arrange
        var did = "did:plc:test123";
        var signedData = new byte[0];
        Func<byte[], byte[]> signer = (hash) => {
            signedData = hash;
            return hash;
        };
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Act
        repo.CreateRecord("app.bsky.feed.post/3kj1abc", CreateTestRecord("Test", "app.bsky.feed.post"));
        repo.Commit(signer);

        // Assert
        Assert.NotNull(repo.CurrentCommit);
        Assert.NotNull(repo.CurrentCommit.Signature);
        Assert.True(signedData.Length > 0);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void EmptyRepository_HasNoRecords()
    {
        // Arrange & Act
        var did = "did:plc:test123";
        var repo = new MstRepository(did);

        // Assert
        Assert.Empty(repo.ListRecords());
        var (nodeCount, recordCount) = repo.GetStats();
        Assert.Equal(0, recordCount);
    }

    [Fact]
    public void SingleRecord_Operations_WorkCorrectly()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        var record = CreateTestRecord("Only post", "app.bsky.feed.post");
        var path = "app.bsky.feed.post/3kj1abc";

        // Act
        repo.CreateRecord(path, record);
        repo.Commit(signer);

        // Assert
        Assert.Single(repo.ListRecords());
        Assert.Equal(record, repo.GetRecord(path));
    }

    [Fact]
    public void LargeNumberOfRecords_HandledCorrectly()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        var recordCount = 100;

        // Act
        for (int i = 0; i < recordCount; i++)
        {
            var record = CreateTestRecord($"Post {i}", "app.bsky.feed.post");
            repo.CreateRecord($"app.bsky.feed.post/3kj{i:D5}", record);
        }
        repo.Commit(signer);

        // Assert
        Assert.Equal(recordCount, repo.ListRecords().Count);
        var (_, records) = repo.GetStats();
        Assert.Equal(recordCount, records);
    }

    [Fact]
    public void RecordPath_WithSpecialCharacters_HandledCorrectly()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        var record = CreateTestRecord("Test", "app.bsky.feed.post");
        
        // Paths with various characters (following AT Protocol TID format)
        var paths = new[]
        {
            "app.bsky.feed.post/3kj1abc",
            "app.bsky.feed.post/3kj2def",
            "app.bsky.actor.profile/self",
            "app.bsky.feed.like/3kj3xyz"
        };

        // Act
        foreach (var path in paths)
        {
            repo.CreateRecord(path, record);
        }
        repo.Commit(signer);

        // Assert
        Assert.Equal(paths.Length, repo.ListRecords().Count);
        foreach (var path in paths)
        {
            Assert.NotNull(repo.GetRecord(path));
        }
    }

    [Fact]
    public void EmptyRecordData_ThrowsOrHandlesGracefully()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        var emptyRecord = Array.Empty<byte>();

        // Act
        repo.CreateRecord("app.bsky.feed.post/3kj1abc", emptyRecord);
        repo.Commit(signer);
        var retrieved = repo.GetRecord("app.bsky.feed.post/3kj1abc");

        // Assert - Should handle empty data
        Assert.NotNull(retrieved);
        Assert.Empty(retrieved);
    }

    #endregion

    #region File I/O Tests

    [Fact]
    public void SaveToFile_ThenLoadFromFile_PreservesAllData()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        
        repo.CreateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Post 1", "app.bsky.feed.post"));
        repo.CreateRecord("app.bsky.feed.post/3kj1bbb", CreateTestRecord("Post 2", "app.bsky.feed.post"));
        repo.CreateRecord("app.bsky.actor.profile/self", CreateTestRecord("Profile", "app.bsky.actor.profile"));
        repo.Commit(signer);
        
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            repo.SaveToFile(tempFile);
            var loadedRepo = MstRepository.LoadFromFile(tempFile);

            // Assert
            Assert.Equal(repo.Did, loadedRepo.Did);
            Assert.Equal(repo.ListRecords().Count, loadedRepo.ListRecords().Count);
            
            foreach (var key in repo.ListRecords())
            {
                var original = repo.GetRecord(key);
                var loaded = loadedRepo.GetRecord(key);
                Assert.Equal(original, loaded);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void SaveToFile_BeforeCommit_ThrowsException()
    {
        // Arrange
        var did = "did:plc:test123";
        var repo = new MstRepository(did);
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act & Assert
            var exception = Assert.Throws<Exception>(() => repo.SaveToFile(tempFile));
            Assert.Equal("No commit to save. Call Commit() first.", exception.Message);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadFromFile_WithMultipleCommits_LoadsLatestState()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        
        repo.CreateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Post 1", "app.bsky.feed.post"));
        repo.Commit(signer);
        
        repo.CreateRecord("app.bsky.feed.post/3kj1bbb", CreateTestRecord("Post 2", "app.bsky.feed.post"));
        repo.Commit(signer);
        
        repo.UpdateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Updated Post 1", "app.bsky.feed.post"));
        repo.Commit(signer);
        
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            repo.SaveToFile(tempFile);
            var loadedRepo = MstRepository.LoadFromFile(tempFile);

            // Assert
            Assert.Equal(2, loadedRepo.ListRecords().Count);
            var record = loadedRepo.GetRecord("app.bsky.feed.post/3kj1aaa");
            Assert.NotNull(record);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    #endregion

    #region Repository State Management Tests

    [Fact]
    public void ListRecords_ReturnsCorrectCount_AfterOperations()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Act & Assert - Initially empty
        Assert.Empty(repo.ListRecords());

        // Add records
        repo.CreateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Post 1", "app.bsky.feed.post"));
        repo.CreateRecord("app.bsky.feed.post/3kj1bbb", CreateTestRecord("Post 2", "app.bsky.feed.post"));
        repo.Commit(signer);
        Assert.Equal(2, repo.ListRecords().Count);

        // Delete one
        repo.DeleteRecord("app.bsky.feed.post/3kj1aaa");
        repo.Commit(signer);
        Assert.Single(repo.ListRecords());

        // Add more
        repo.CreateRecord("app.bsky.feed.post/3kj1ccc", CreateTestRecord("Post 3", "app.bsky.feed.post"));
        repo.CreateRecord("app.bsky.feed.post/3kj1ddd", CreateTestRecord("Post 4", "app.bsky.feed.post"));
        repo.Commit(signer);
        Assert.Equal(3, repo.ListRecords().Count);
    }

    [Fact]
    public void GetStats_ReturnsAccurateRecordCount()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Act
        repo.CreateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Post 1", "app.bsky.feed.post"));
        repo.CreateRecord("app.bsky.feed.post/3kj1bbb", CreateTestRecord("Post 2", "app.bsky.feed.post"));
        repo.CreateRecord("app.bsky.actor.profile/self", CreateTestRecord("Profile", "app.bsky.actor.profile"));
        repo.Commit(signer);

        var (nodeCount, recordCount) = repo.GetStats();

        // Assert
        Assert.Equal(3, recordCount);
        Assert.True(nodeCount > 0);
    }

    [Fact]
    public void GetStats_NodeCount_GreaterThanZero_WithRecords()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Act
        for (int i = 0; i < 10; i++)
        {
            repo.CreateRecord($"app.bsky.feed.post/3kj{i:D5}", CreateTestRecord($"Post {i}", "app.bsky.feed.post"));
        }
        repo.Commit(signer);

        var (nodeCount, recordCount) = repo.GetStats();

        // Assert
        Assert.Equal(10, recordCount);
        Assert.True(nodeCount > 0, "Node count should be greater than 0");
        Assert.True(nodeCount >= recordCount, "Node count should typically be >= record count in MST");
    }

    [Fact]
    public void CreateForNewUser_InitializesCorrectly()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;

        // Act
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Assert
        Assert.Equal(did, repo.Did);
        Assert.Equal(1, repo.SequenceNumber);
        Assert.NotNull(repo.CurrentCommit);
        Assert.Equal(did, repo.CurrentCommit.Did);
        Assert.NotNull(repo.CurrentCommit.Rev);
        Assert.NotNull(repo.CurrentCommit.Signature);
        Assert.Empty(repo.ListRecords());
    }

    #endregion

    #region Firehose Event Generation Tests

    [Fact]
    public void FirehoseEvent_ContainsAllOperations()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Act
        repo.CreateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Post 1", "app.bsky.feed.post"));
        repo.CreateRecord("app.bsky.feed.post/3kj1bbb", CreateTestRecord("Post 2", "app.bsky.feed.post"));
        repo.CreateRecord("app.bsky.actor.profile/self", CreateTestRecord("Profile", "app.bsky.actor.profile"));
        
        var event1 = repo.Commit(signer);

        // Assert
        Assert.Equal(3, event1.Ops.Count);
        Assert.All(event1.Ops, op => Assert.Equal("create", op.Action));
        Assert.Contains(event1.Ops, op => op.Path == "app.bsky.feed.post/3kj1aaa");
        Assert.Contains(event1.Ops, op => op.Path == "app.bsky.feed.post/3kj1bbb");
        Assert.Contains(event1.Ops, op => op.Path == "app.bsky.actor.profile/self");
    }

    [Fact]
    public void FirehoseEvent_DiffBlocks_ContainChangedData()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Act
        repo.CreateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Post 1", "app.bsky.feed.post"));
        var event1 = repo.Commit(signer);

        // Assert
        Assert.NotNull(event1.Blocks);
        Assert.True(event1.Blocks.Length > 0, "Blocks should contain CAR data");
    }

    [Fact]
    public void FirehoseEvent_TracksOperationTypes()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Act - Create
        repo.CreateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Post 1", "app.bsky.feed.post"));
        var createEvent = repo.Commit(signer);

        // Act - Update
        repo.UpdateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Updated Post 1", "app.bsky.feed.post"));
        var updateEvent = repo.Commit(signer);

        // Act - Delete
        repo.DeleteRecord("app.bsky.feed.post/3kj1aaa");
        var deleteEvent = repo.Commit(signer);

        // Assert
        Assert.Single(createEvent.Ops);
        Assert.Equal("create", createEvent.Ops[0].Action);
        Assert.NotNull(createEvent.Ops[0].Cid);

        Assert.Single(updateEvent.Ops);
        Assert.Equal("update", updateEvent.Ops[0].Action);
        Assert.NotNull(updateEvent.Ops[0].Cid);

        Assert.Single(deleteEvent.Ops);
        Assert.Equal("delete", deleteEvent.Ops[0].Action);
    }

    [Fact]
    public void FirehoseEvent_HasCorrectTimestamp()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        var beforeCommit = DateTime.UtcNow;

        // Act
        repo.CreateRecord("app.bsky.feed.post/3kj1abc", CreateTestRecord("Test", "app.bsky.feed.post"));
        var event1 = repo.Commit(signer);
        var afterCommit = DateTime.UtcNow;

        // Assert
        Assert.NotNull(event1.Time);
        var timestamp = DateTime.Parse(event1.Time, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.True(timestamp >= beforeCommit.AddSeconds(-2), $"Timestamp {timestamp} should be >= {beforeCommit.AddSeconds(-2)}");
        Assert.True(timestamp <= afterCommit.AddSeconds(2), $"Timestamp {timestamp} should be <= {afterCommit.AddSeconds(2)}");
    }

    [Fact]
    public void FirehoseEvent_TracksSinceRev()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);
        var initialRev = repo.CurrentCommit!.Rev;

        // Act
        repo.CreateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Post 1", "app.bsky.feed.post"));
        var event1 = repo.Commit(signer);
        
        repo.CreateRecord("app.bsky.feed.post/3kj1bbb", CreateTestRecord("Post 2", "app.bsky.feed.post"));
        var event2 = repo.Commit(signer);

        // Assert
        Assert.Equal(initialRev, event1.Since); // First user commit references initial commit
        Assert.Equal(event1.Rev, event2.Since); // Second commit references first
    }

    #endregion

    #region Multiple Path Operations Tests

    [Fact]
    public void CreateRecords_InDifferentCollections_Simultaneously()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Act
        repo.CreateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Post", "app.bsky.feed.post"));
        repo.CreateRecord("app.bsky.feed.like/3kj1bbb", CreateTestRecord("Like", "app.bsky.feed.like"));
        repo.CreateRecord("app.bsky.actor.profile/self", CreateTestRecord("Profile", "app.bsky.actor.profile"));
        repo.CreateRecord("app.bsky.graph.follow/3kj1ccc", CreateTestRecord("Follow", "app.bsky.graph.follow"));
        
        var event1 = repo.Commit(signer);

        // Assert
        Assert.Equal(4, event1.Ops.Count);
        Assert.Equal(4, repo.ListRecords().Count);
        
        var records = repo.ListRecords();
        Assert.Contains("app.bsky.feed.post/3kj1aaa", records);
        Assert.Contains("app.bsky.feed.like/3kj1bbb", records);
        Assert.Contains("app.bsky.actor.profile/self", records);
        Assert.Contains("app.bsky.graph.follow/3kj1ccc", records);
    }

    [Fact]
    public void MixedOperations_AcrossDifferentRecordTypes_InOneCommit()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Setup initial records
        repo.CreateRecord("app.bsky.feed.post/3kj1aaa", CreateTestRecord("Post 1", "app.bsky.feed.post"));
        repo.CreateRecord("app.bsky.feed.post/3kj1bbb", CreateTestRecord("Post 2", "app.bsky.feed.post"));
        repo.CreateRecord("app.bsky.actor.profile/self", CreateTestRecord("Profile", "app.bsky.actor.profile"));
        repo.Commit(signer);

        // Act - Mixed operations
        repo.CreateRecord("app.bsky.feed.post/3kj1ccc", CreateTestRecord("Post 3", "app.bsky.feed.post"));
        repo.UpdateRecord("app.bsky.actor.profile/self", CreateTestRecord("Updated Profile", "app.bsky.actor.profile"));
        repo.DeleteRecord("app.bsky.feed.post/3kj1aaa");
        
        var event1 = repo.Commit(signer);

        // Assert
        Assert.Equal(3, event1.Ops.Count);
        Assert.Contains(event1.Ops, op => op.Action == "create" && op.Path == "app.bsky.feed.post/3kj1ccc");
        Assert.Contains(event1.Ops, op => op.Action == "update" && op.Path == "app.bsky.actor.profile/self");
        Assert.Contains(event1.Ops, op => op.Action == "delete" && op.Path == "app.bsky.feed.post/3kj1aaa");
        
        Assert.Equal(3, repo.ListRecords().Count);
    }

    [Fact]
    public void SortedPaths_MaintainedInMst()
    {
        // Arrange
        var did = "did:plc:test123";
        Func<byte[], byte[]> signer = (hash) => hash;
        var repo = MstRepository.CreateForNewUser(did, signer);

        // Act - Add in non-alphabetical order
        var paths = new[]
        {
            "app.bsky.feed.post/3kj1zzz",
            "app.bsky.feed.post/3kj1aaa",
            "app.bsky.feed.post/3kj1mmm",
            "app.bsky.actor.profile/self",
            "app.bsky.feed.post/3kj1bbb"
        };

        foreach (var path in paths)
        {
            repo.CreateRecord(path, CreateTestRecord($"Record for {path}", "app.bsky.feed.post"));
        }
        repo.Commit(signer);

        // Assert - MST should maintain sorted order
        var records = repo.ListRecords();
        var sortedPaths = paths.OrderBy(x => x).ToList();
        
        Assert.Equal(sortedPaths.Count, records.Count);
        for (int i = 0; i < sortedPaths.Count; i++)
        {
            Assert.Equal(sortedPaths[i], records[i]);
        }
    }

    #endregion

    /// <summary>
    /// Helper method to create a test record with DAG-CBOR encoding.
    /// </summary>
    private byte[] CreateTestRecord(string text, string recordType)
    {
        using var ms = new MemoryStream();
        
        var dict = new Dictionary<string, DagCborObject>
        {
            ["$type"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = recordType
            },
            ["text"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = text
            },
            ["createdAt"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            }
        };
        
        var obj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = dict
        };
        
        DagCborObject.WriteToStream(obj, ms);
        return ms.ToArray();
    }
}
