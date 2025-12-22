namespace dnproto.tests.sdk.mst;

using dnproto.sdk.mst;
using dnproto.sdk.repo;
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
