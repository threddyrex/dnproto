namespace dnproto.tests.sdk.mst;

using dnproto.sdk.mst;
using dnproto.sdk.repo;
using System.Linq;

/// <summary>
/// Tests that compare Bluesky-created repositories with dnproto's MstRepository implementation.
/// Each test simulates a step in the Bluesky account workflow and compares the result
/// against actual Bluesky CAR files.
/// </summary>
public class MstTest
{
    private const string TEST_DID = "did:plc:msttest123456789";
    private const string TEST_DATA_DIR = "sdk/mst"; // CAR files are in the same directory as this test file

    #region Helper Methods

    /// <summary>
    /// Create a simple signing function for testing (identity signer).
    /// In production, this would use actual cryptographic signing.
    /// </summary>
    private Func<byte[], byte[]> GetTestSigner()
    {
        return (hash) => hash; // Identity signer for testing
    }

    /// <summary>
    /// Create a test profile record.
    /// </summary>
    private byte[] CreateProfileRecord(string? displayName = null, string? description = null)
    {
        using var ms = new MemoryStream();
        
        var dict = new Dictionary<string, DagCborObject>
        {
            ["$type"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = "app.bsky.actor.profile"
            }
        };

        if (!string.IsNullOrEmpty(displayName))
        {
            dict["displayName"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = displayName
            };
        }

        if (!string.IsNullOrEmpty(description))
        {
            dict["description"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = description
            };
        }

        var obj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = dict
        };
        
        DagCborObject.WriteToStream(obj, ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Create a test post record.
    /// </summary>
    private byte[] CreatePostRecord(string text, string? createdAt = null)
    {
        using var ms = new MemoryStream();
        
        var dict = new Dictionary<string, DagCborObject>
        {
            ["$type"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = "app.bsky.feed.post"
            },
            ["text"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = text
            },
            ["createdAt"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = createdAt ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
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

    /// <summary>
    /// Create a test like record.
    /// </summary>
    private byte[] CreateLikeRecord(string subjectUri, string subjectCid, string? createdAt = null)
    {
        using var ms = new MemoryStream();
        
        // Create subject (nested object)
        var subjectDict = new Dictionary<string, DagCborObject>
        {
            ["uri"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = subjectUri
            },
            ["cid"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = subjectCid
            }
        };
        
        var subjectObj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = subjectDict
        };

        // Create like record
        var dict = new Dictionary<string, DagCborObject>
        {
            ["$type"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = "app.bsky.feed.like"
            },
            ["subject"] = subjectObj,
            ["createdAt"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = createdAt ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
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

    /// <summary>
    /// Create a test reply post record.
    /// </summary>
    private byte[] CreateReplyRecord(string text, string parentUri, string parentCid, string rootUri, string rootCid, string? createdAt = null)
    {
        using var ms = new MemoryStream();
        
        // Create parent reference
        var parentDict = new Dictionary<string, DagCborObject>
        {
            ["uri"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = parentUri
            },
            ["cid"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = parentCid
            }
        };
        
        var parentObj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = parentDict
        };

        // Create root reference
        var rootDict = new Dictionary<string, DagCborObject>
        {
            ["uri"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = rootUri
            },
            ["cid"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = rootCid
            }
        };
        
        var rootObj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = rootDict
        };

        // Create reply object
        var replyDict = new Dictionary<string, DagCborObject>
        {
            ["parent"] = parentObj,
            ["root"] = rootObj
        };
        
        var replyObj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = replyDict
        };

        // Create post record with reply
        var dict = new Dictionary<string, DagCborObject>
        {
            ["$type"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = "app.bsky.feed.post"
            },
            ["text"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = text
            },
            ["reply"] = replyObj,
            ["createdAt"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                Value = createdAt ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
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

    /// <summary>
    /// Compare two repositories and log differences.
    /// </summary>
    private void CompareRepositories(MstRepository blueskyRepo, MstRepository dnprotoRepo, string stepName)
    {
        // Compare CAR header - version
        var blueskyVersion = blueskyRepo.GetCarVersion();
        var dnprotoVersion = dnprotoRepo.GetCarVersion();
        Assert.Equal(blueskyVersion, dnprotoVersion);
        Assert.Equal(1, dnprotoVersion); // AT Protocol uses CAR version 1

        // Compare CAR header - roots array
        var blueskyRoots = blueskyRepo.GetRoots();
        var dnprotoRoots = dnprotoRepo.GetRoots();
        Assert.Equal(blueskyRoots.Length, dnprotoRoots.Length);
        Assert.Single(dnprotoRoots); // Should have exactly one root (the commit CID)
        Assert.Single(blueskyRoots); // Bluesky should also have exactly one root
        
        // Verify roots contain valid CIDs
        foreach (var root in dnprotoRoots)
        {
            Assert.NotNull(root);
            Assert.NotNull(root.Base32); // Should have valid base32 representation
        }

        // Compare commit blocks - verify fields exist and check values where appropriate
        Assert.NotNull(blueskyRepo.CurrentCommit);
        Assert.NotNull(dnprotoRepo.CurrentCommit);
        
        // did - Will differ (different accounts), but should exist and be valid
        Assert.NotNull(blueskyRepo.CurrentCommit.Did);
        Assert.NotEmpty(blueskyRepo.CurrentCommit.Did);
        Assert.NotNull(dnprotoRepo.CurrentCommit.Did);
        Assert.NotEmpty(dnprotoRepo.CurrentCommit.Did);
        Assert.Equal(TEST_DID, dnprotoRepo.CurrentCommit.Did); // dnproto should use our test DID
        
        // version - Should match (both v3 repos)
        Assert.Equal(3, dnprotoRepo.CurrentCommit.Version);
        Assert.Equal(blueskyRepo.CurrentCommit.Version, dnprotoRepo.CurrentCommit.Version);
        
        // rev - Will differ (timestamp-based), but should exist
        Assert.NotNull(dnprotoRepo.CurrentCommit.Rev);
        Assert.NotEmpty(dnprotoRepo.CurrentCommit.Rev);
        
        // prev - Should match (typically null for v3 repos)
        Assert.Equal(blueskyRepo.CurrentCommit.PrevCid, dnprotoRepo.CurrentCommit.PrevCid);
        
        // sig - Will differ (different signing), but should exist
        Assert.NotNull(dnprotoRepo.CurrentCommit.Signature);
        Assert.NotEmpty(dnprotoRepo.CurrentCommit.Signature);
        
        // CommitCid - Should exist and match the root in the CAR header
        Assert.NotNull(dnprotoRepo.CurrentCommit.CommitCid);
        Assert.Equal(dnprotoRoots[0].Base32, dnprotoRepo.CurrentCommit.CommitCid.Base32);
        
        // Compare record counts
        var blueskyRecords = blueskyRepo.ListRecords().OrderBy(x => x).ToList();
        var dnprotoRecords = dnprotoRepo.ListRecords().OrderBy(x => x).ToList();

        Assert.Equal(blueskyRecords.Count, dnprotoRecords.Count);
        
        // data - Should exist (MST root CID) when records exist, may differ in value
        if (dnprotoRecords.Count > 0)
        {
            Assert.NotNull(dnprotoRepo.CurrentCommit.DataCid);
        }
        
        // Compare record paths
        for (int i = 0; i < blueskyRecords.Count; i++)
        {
            Assert.Equal(blueskyRecords[i], dnprotoRecords[i]);
        }

        // Compare MST structure
        var (blueskyNodeCount, blueskyRecordCount) = blueskyRepo.GetStats();
        var (dnprotoNodeCount, dnprotoRecordCount) = dnprotoRepo.GetStats();
        
        Assert.Equal(blueskyRecordCount, dnprotoRecordCount);
        
        // Note: Node counts may differ slightly due to implementation details
        // For empty repos, node count can be 0
        if (blueskyRecordCount > 0)
        {
            Assert.True(blueskyNodeCount > 0, $"{stepName}: Bluesky repo should have nodes when records exist");
            Assert.True(dnprotoNodeCount > 0, $"{stepName}: dnproto repo should have nodes when records exist");
        }
        
        // Compare root CID
        var blueskyRootCid = blueskyRepo.Mst.Root?.ComputeCid();
        var dnprotoRootCid = dnprotoRepo.Mst.Root?.ComputeCid();
        
        if (blueskyRecordCount > 0)
        {
            Assert.NotNull(blueskyRootCid);
            Assert.NotNull(dnprotoRootCid);
            // Note: CIDs may differ if record data or timestamps differ
        }
    }

    /// <summary>
    /// Load a Bluesky CAR file if it exists, otherwise skip the test.
    /// </summary>
    private MstRepository? LoadBlueskyRepoOrSkip(string filename, [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
    {
        // Get the directory where this test file is located
        var testDir = Path.GetDirectoryName(sourceFilePath) ?? "";
        var filePath = Path.Combine(testDir, filename);
        
        if (!File.Exists(filePath))
        {
            throw new Exception($"Bluesky CAR file not found: {filePath}");
        }

        return MstRepository.LoadFromFile(filePath);
    }

    #endregion

    #region Step 1: New Account

    [Fact]
    public void Step01_NewAccount_MatchesBluesky()
    {
        // NOTE: CAR file structure vs user records
        // A new account's CAR file contains 2 blocks:
        //   1. Commit block (with fields: did, rev, sig, data, prev)
        //   2. Empty MST root node block
        // However, ListRecords() returns 0 because there are no user-facing records yet.
        //
        // CAR blocks = infrastructure (commit + MST nodes + records)
        // Records = only user content (posts, likes, profiles, etc.)
        
        // Arrange
        var blueskyRepo = LoadBlueskyRepoOrSkip("msttest-01-new.car");
        if (blueskyRepo == null)
        {
            Assert.NotNull(blueskyRepo);
            // Skip test if CAR file not available
            return;
        }

        var signer = GetTestSigner();

        // Act - Create a new dnproto repository
        var dnprotoRepo = MstRepository.CreateForNewUser(TEST_DID, signer);

        // Assert - Compare repositories
        CompareRepositories(blueskyRepo, dnprotoRepo, "Step01_NewAccount");
        
        // A new account should have no user records (infrastructure blocks exist but aren't counted)
        Assert.Empty(dnprotoRepo.ListRecords());
    }

    #endregion

    #region Step 2: Login

    [Fact]
    public void Step02_Login_MatchesBluesky()
    {
        // Arrange
        var blueskyRepo = LoadBlueskyRepoOrSkip("msttest-02-login.car");
        if (blueskyRepo == null)
        {
            Assert.NotNull(blueskyRepo);
            return;
        }

        var signer = GetTestSigner();

        // Act - Login doesn't change the repository state
        var dnprotoRepo = MstRepository.CreateForNewUser(TEST_DID, signer);

        // Assert
        CompareRepositories(blueskyRepo, dnprotoRepo, "Step02_Login");
        
        // Login should not add any records
        Assert.Empty(dnprotoRepo.ListRecords());
    }

    #endregion

    #region Step 3: Set Profile

    [Fact]
    public void Step03_SetProfile_MatchesBluesky()
    {
        // Arrange
        var blueskyRepo = LoadBlueskyRepoOrSkip("msttest-03-setprofile.car");
        Assert.NotNull(blueskyRepo);
        if (blueskyRepo == null)
        {
            return;
        }

        var signer = GetTestSigner();

        // Act - Create repository and set profile
        var dnprotoRepo = MstRepository.CreateForNewUser(TEST_DID, signer);
        
        var profileRecord = CreateProfileRecord(
            displayName: "Test User",
            description: "Testing dnproto MST"
        );
        
        dnprotoRepo.CreateRecord("app.bsky.actor.profile/self", profileRecord);
        dnprotoRepo.Commit(signer);

        // Assert
        var dnprotoRecords = dnprotoRepo.ListRecords();
        Assert.Single(dnprotoRecords);
        Assert.Contains("app.bsky.actor.profile/self", dnprotoRecords);
        
        // Compare with Bluesky repo
        var blueskyRecords = blueskyRepo.ListRecords();
        Assert.Equal(blueskyRecords.Count, dnprotoRecords.Count);
        
        // Compare the actual profile record type
        var blueskyProfileData = blueskyRepo.GetRecord("app.bsky.actor.profile/self");
        var dnprotoProfileData = dnprotoRepo.GetRecord("app.bsky.actor.profile/self");
        
        Assert.NotNull(blueskyProfileData);
        Assert.NotNull(dnprotoProfileData);
        
        // Parse both records to check the $type field
        using var blueskyMs = new MemoryStream(blueskyProfileData);
        var blueskyProfileObj = DagCborObject.ReadFromStream(blueskyMs);
        
        using var dnprotoMs = new MemoryStream(dnprotoProfileData);
        var dnprotoProfileObj = DagCborObject.ReadFromStream(dnprotoMs);
        
        // Both should be maps with a $type field
        Assert.True(blueskyProfileObj.Value is Dictionary<string, DagCborObject>);
        Assert.True(dnprotoProfileObj.Value is Dictionary<string, DagCborObject>);
        
        var blueskyDict = (Dictionary<string, DagCborObject>)blueskyProfileObj.Value;
        var dnprotoDict = (Dictionary<string, DagCborObject>)dnprotoProfileObj.Value;
        
        Assert.True(blueskyDict.ContainsKey("$type"));
        Assert.True(dnprotoDict.ContainsKey("$type"));
        
        var blueskyType = blueskyDict["$type"].Value as string;
        var dnprotoType = dnprotoDict["$type"].Value as string;
        
        Assert.Equal("app.bsky.actor.profile", blueskyType);
        Assert.Equal("app.bsky.actor.profile", dnprotoType);
        Assert.Equal(blueskyType, dnprotoType);
    }

    #endregion

    #region Step 4: First Post

    [Fact]
    public void Step04_FirstPost_MatchesBluesky()
    {
        // Arrange
        var blueskyRepo = LoadBlueskyRepoOrSkip("msttest-04-firstpost.car");
        Assert.NotNull(blueskyRepo);
        if (blueskyRepo == null)
        {
            return;
        }

        var signer = GetTestSigner();

        // Act - Create repository with profile and first post
        var dnprotoRepo = MstRepository.CreateForNewUser(TEST_DID, signer);
        
        // Add profile
        var profileRecord = CreateProfileRecord(
            displayName: "Test User",
            description: "Testing dnproto MST"
        );
        dnprotoRepo.CreateRecord("app.bsky.actor.profile/self", profileRecord);
        dnprotoRepo.Commit(signer);
        
        // Add first post
        var postRecord = CreatePostRecord("Hello from dnproto!");
        var postTid = RecordKey.GenerateTid();
        dnprotoRepo.CreateRecord($"app.bsky.feed.post/{postTid}", postRecord);
        dnprotoRepo.Commit(signer);

        // Assert
        var dnprotoRecords = dnprotoRepo.ListRecords();
        Assert.Equal(2, dnprotoRecords.Count);
        Assert.Contains("app.bsky.actor.profile/self", dnprotoRecords);
        Assert.Single(dnprotoRecords, r => r.StartsWith("app.bsky.feed.post/"));
        
        // Compare with Bluesky repo
        var blueskyRecords = blueskyRepo.ListRecords();
        Assert.Equal(blueskyRecords.Count, dnprotoRecords.Count);
    }

    #endregion

    #region Step 5: First Like

    [Fact]
    public void Step05_FirstLike_MatchesBluesky()
    {
        // Arrange
        var blueskyRepo = LoadBlueskyRepoOrSkip("msttest-05-firstlike.car");
        Assert.NotNull(blueskyRepo);
        if (blueskyRepo == null)
        {
            return;
        }

        var signer = GetTestSigner();

        // Act - Create repository with profile, post, and like
        var dnprotoRepo = MstRepository.CreateForNewUser(TEST_DID, signer);
        
        // Add profile
        var profileRecord = CreateProfileRecord(
            displayName: "Test User",
            description: "Testing dnproto MST"
        );
        dnprotoRepo.CreateRecord("app.bsky.actor.profile/self", profileRecord);
        dnprotoRepo.Commit(signer);
        
        // Add first post
        var postRecord = CreatePostRecord("Hello from dnproto!");
        var postTid = RecordKey.GenerateTid();
        var postPath = $"app.bsky.feed.post/{postTid}";
        dnprotoRepo.CreateRecord(postPath, postRecord);
        dnprotoRepo.Commit(signer);
        
        // Add first like (liking someone else's post)
        var likeRecord = CreateLikeRecord(
            subjectUri: "at://did:plc:example123/app.bsky.feed.post/3kj1abc",
            subjectCid: "bafyreiabc123example"
        );
        var likeTid = RecordKey.GenerateTid();
        dnprotoRepo.CreateRecord($"app.bsky.feed.like/{likeTid}", likeRecord);
        dnprotoRepo.Commit(signer);

        // Assert
        var dnprotoRecords = dnprotoRepo.ListRecords();
        Assert.Equal(3, dnprotoRecords.Count);
        Assert.Contains("app.bsky.actor.profile/self", dnprotoRecords);
        Assert.Single(dnprotoRecords, r => r.StartsWith("app.bsky.feed.post/"));
        Assert.Single(dnprotoRecords, r => r.StartsWith("app.bsky.feed.like/"));
        
        // Compare with Bluesky repo
        var blueskyRecords = blueskyRepo.ListRecords();
        Assert.Equal(blueskyRecords.Count, dnprotoRecords.Count);
    }

    #endregion

    #region Step 6: Reply

    [Fact]
    public void Step06_Reply_MatchesBluesky()
    {
        // Arrange
        var blueskyRepo = LoadBlueskyRepoOrSkip("msttest-06-reply.car");
        Assert.NotNull(blueskyRepo);
        if (blueskyRepo == null)
        {
            return;
        }

        var signer = GetTestSigner();

        // Act - Create repository with profile, post, like, and reply
        var dnprotoRepo = MstRepository.CreateForNewUser(TEST_DID, signer);
        
        // Add profile
        var profileRecord = CreateProfileRecord(
            displayName: "Test User",
            description: "Testing dnproto MST"
        );
        dnprotoRepo.CreateRecord("app.bsky.actor.profile/self", profileRecord);
        dnprotoRepo.Commit(signer);
        
        // Add first post
        var postRecord = CreatePostRecord("Hello from dnproto!");
        var postTid = RecordKey.GenerateTid();
        var postPath = $"app.bsky.feed.post/{postTid}";
        dnprotoRepo.CreateRecord(postPath, postRecord);
        dnprotoRepo.Commit(signer);
        
        // Add first like
        var likeRecord = CreateLikeRecord(
            subjectUri: "at://did:plc:example123/app.bsky.feed.post/3kj1abc",
            subjectCid: "bafyreiabc123example"
        );
        var likeTid = RecordKey.GenerateTid();
        dnprotoRepo.CreateRecord($"app.bsky.feed.like/{likeTid}", likeRecord);
        dnprotoRepo.Commit(signer);
        
        // Add reply to someone's post
        var replyRecord = CreateReplyRecord(
            text: "This is a reply",
            parentUri: "at://did:plc:example123/app.bsky.feed.post/3kj1abc",
            parentCid: "bafyreiabc123example",
            rootUri: "at://did:plc:example123/app.bsky.feed.post/3kj1abc",
            rootCid: "bafyreiabc123example"
        );
        var replyTid = RecordKey.GenerateTid();
        dnprotoRepo.CreateRecord($"app.bsky.feed.post/{replyTid}", replyRecord);
        dnprotoRepo.Commit(signer);

        // Assert
        var dnprotoRecords = dnprotoRepo.ListRecords();
        Assert.Equal(4, dnprotoRecords.Count);
        Assert.Contains("app.bsky.actor.profile/self", dnprotoRecords);
        Assert.Equal(2, dnprotoRecords.Count(r => r.StartsWith("app.bsky.feed.post/")));
        Assert.Single(dnprotoRecords, r => r.StartsWith("app.bsky.feed.like/"));
        
        // Compare with Bluesky repo
        var blueskyRecords = blueskyRepo.ListRecords();
        Assert.Equal(blueskyRecords.Count, dnprotoRecords.Count);
    }

    #endregion

    #region Step 7: Handle Change

    [Fact]
    public void Step07_HandleChange_MatchesBluesky()
    {
        // Arrange
        var blueskyRepo = LoadBlueskyRepoOrSkip("msttest-07-handlechange.car");
        Assert.NotNull(blueskyRepo);
        if (blueskyRepo == null)
        {
            return;
        }

        var signer = GetTestSigner();

        // Act - Create repository with all previous records
        // Note: Handle change doesn't modify the repository itself,
        // it's a DID document change. The repository should be the same as step 6.
        var dnprotoRepo = MstRepository.CreateForNewUser(TEST_DID, signer);
        
        // Add profile
        var profileRecord = CreateProfileRecord(
            displayName: "Test User",
            description: "Testing dnproto MST"
        );
        dnprotoRepo.CreateRecord("app.bsky.actor.profile/self", profileRecord);
        dnprotoRepo.Commit(signer);
        
        // Add first post
        var postRecord = CreatePostRecord("Hello from dnproto!");
        var postTid = RecordKey.GenerateTid();
        var postPath = $"app.bsky.feed.post/{postTid}";
        dnprotoRepo.CreateRecord(postPath, postRecord);
        dnprotoRepo.Commit(signer);
        
        // Add first like
        var likeRecord = CreateLikeRecord(
            subjectUri: "at://did:plc:example123/app.bsky.feed.post/3kj1abc",
            subjectCid: "bafyreiabc123example"
        );
        var likeTid = RecordKey.GenerateTid();
        dnprotoRepo.CreateRecord($"app.bsky.feed.like/{likeTid}", likeRecord);
        dnprotoRepo.Commit(signer);
        
        // Add reply
        var replyRecord = CreateReplyRecord(
            text: "This is a reply",
            parentUri: "at://did:plc:example123/app.bsky.feed.post/3kj1abc",
            parentCid: "bafyreiabc123example",
            rootUri: "at://did:plc:example123/app.bsky.feed.post/3kj1abc",
            rootCid: "bafyreiabc123example"
        );
        var replyTid = RecordKey.GenerateTid();
        dnprotoRepo.CreateRecord($"app.bsky.feed.post/{replyTid}", replyRecord);
        dnprotoRepo.Commit(signer);

        // Assert
        var dnprotoRecords = dnprotoRepo.ListRecords();
        Assert.Equal(4, dnprotoRecords.Count);
        
        // Compare with Bluesky repo
        var blueskyRecords = blueskyRepo.ListRecords();
        Assert.Equal(blueskyRecords.Count, dnprotoRecords.Count);
    }

    #endregion

    #region Integration Test - Full Workflow

    [Fact]
    public void FullWorkflow_AllSteps_RepositoryIsValid()
    {
        // This test runs through all steps in sequence without comparing to Bluesky,
        // just to ensure the dnproto implementation works correctly.

        var signer = GetTestSigner();
        var repo = MstRepository.CreateForNewUser(TEST_DID, signer);

        // Step 1: New account (empty)
        Assert.Empty(repo.ListRecords());

        // Step 2: Login (no change)
        Assert.Empty(repo.ListRecords());

        // Step 3: Set profile
        var profileRecord = CreateProfileRecord("Test User", "Testing dnproto MST");
        repo.CreateRecord("app.bsky.actor.profile/self", profileRecord);
        repo.Commit(signer);
        Assert.Single(repo.ListRecords());

        // Step 4: First post
        var postRecord = CreatePostRecord("Hello from dnproto!");
        var postTid = RecordKey.GenerateTid();
        repo.CreateRecord($"app.bsky.feed.post/{postTid}", postRecord);
        repo.Commit(signer);
        Assert.Equal(2, repo.ListRecords().Count);

        // Step 5: First like
        var likeRecord = CreateLikeRecord(
            "at://did:plc:example123/app.bsky.feed.post/3kj1abc",
            "bafyreiabc123example"
        );
        var likeTid = RecordKey.GenerateTid();
        repo.CreateRecord($"app.bsky.feed.like/{likeTid}", likeRecord);
        repo.Commit(signer);
        Assert.Equal(3, repo.ListRecords().Count);

        // Step 6: Reply
        var replyRecord = CreateReplyRecord(
            "This is a reply",
            "at://did:plc:example123/app.bsky.feed.post/3kj1abc",
            "bafyreiabc123example",
            "at://did:plc:example123/app.bsky.feed.post/3kj1abc",
            "bafyreiabc123example"
        );
        var replyTid = RecordKey.GenerateTid();
        repo.CreateRecord($"app.bsky.feed.post/{replyTid}", replyRecord);
        repo.Commit(signer);
        Assert.Equal(4, repo.ListRecords().Count);

        // Step 7: Handle change (no repository change)
        Assert.Equal(4, repo.ListRecords().Count);

        // Verify final state
        var records = repo.ListRecords();
        Assert.Contains("app.bsky.actor.profile/self", records);
        Assert.Equal(2, records.Count(r => r.StartsWith("app.bsky.feed.post/")));
        Assert.Single(records, r => r.StartsWith("app.bsky.feed.like/"));

        // Verify repository can be saved and loaded
        var tempFile = Path.GetTempFileName();
        try
        {
            repo.SaveToFile(tempFile);
            var loadedRepo = MstRepository.LoadFromFile(tempFile);
            
            Assert.Equal(records.Count, loadedRepo.ListRecords().Count);
            Assert.Equal(repo.Did, loadedRepo.Did);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    #endregion
}
