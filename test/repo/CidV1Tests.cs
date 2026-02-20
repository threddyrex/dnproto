namespace dnproto.tests.repo;

using dnproto.repo;

public class CidV1Tests
{
    [Fact]
    public void WriteAndReadCid_RoundTrip_DagCbor()
    {
        // Arrange - Create a CID with dag-cbor multicodec
        var originalCid = new CidV1
        {
            Version = new VarInt { Value = 1 },
            Multicodec = new VarInt { Value = 0x71 }, // dag-cbor
            HashFunction = new VarInt { Value = 0x12 }, // sha2-256
            DigestSize = new VarInt { Value = 32 },
            DigestBytes = new byte[32] { 
                0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
                0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
                0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20
            },
            AllBytes = new byte[36], // Will be set properly by ReadCid
            Base32 = "" // Will be set properly by ReadCid
        };

        using var writeStream = new MemoryStream();

        // Act - Write
        CidV1.WriteCid(writeStream, originalCid);

        // Act - Read
        writeStream.Position = 0;
        var readCid = CidV1.ReadCid(writeStream);

        // Assert
        Assert.Equal(originalCid.Version.Value, readCid.Version.Value);
        Assert.Equal(originalCid.Multicodec.Value, readCid.Multicodec.Value);
        Assert.Equal(originalCid.HashFunction.Value, readCid.HashFunction.Value);
        Assert.Equal(originalCid.DigestSize.Value, readCid.DigestSize.Value);
        Assert.Equal(originalCid.DigestBytes, readCid.DigestBytes);
    }

    [Fact]
    public void WriteAndReadCid_RoundTrip_Raw()
    {
        // Arrange - Create a CID with raw multicodec
        var originalCid = new CidV1
        {
            Version = new VarInt { Value = 1 },
            Multicodec = new VarInt { Value = 0x55 }, // raw
            HashFunction = new VarInt { Value = 0x12 }, // sha2-256
            DigestSize = new VarInt { Value = 32 },
            DigestBytes = new byte[32] { 
                0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88,
                0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00,
                0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77,
                0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF
            },
            AllBytes = new byte[36], // Will be set properly by ReadCid
            Base32 = "" // Will be set properly by ReadCid
        };

        using var writeStream = new MemoryStream();

        // Act - Write
        CidV1.WriteCid(writeStream, originalCid);

        // Act - Read
        writeStream.Position = 0;
        var readCid = CidV1.ReadCid(writeStream);

        // Assert
        Assert.Equal(originalCid.Version.Value, readCid.Version.Value);
        Assert.Equal(originalCid.Multicodec.Value, readCid.Multicodec.Value);
        Assert.Equal(originalCid.HashFunction.Value, readCid.HashFunction.Value);
        Assert.Equal(originalCid.DigestSize.Value, readCid.DigestSize.Value);
        Assert.Equal(originalCid.DigestBytes, readCid.DigestBytes);
    }

    [Fact]
    public void WriteAndReadCid_RoundTrip_AllZeros()
    {
        // Arrange - Create a CID with all zero digest bytes
        var originalCid = new CidV1
        {
            Version = new VarInt { Value = 1 },
            Multicodec = new VarInt { Value = 0x71 }, // dag-cbor
            HashFunction = new VarInt { Value = 0x12 }, // sha2-256
            DigestSize = new VarInt { Value = 32 },
            DigestBytes = new byte[32], // All zeros
            AllBytes = new byte[36],
            Base32 = ""
        };

        using var writeStream = new MemoryStream();

        // Act - Write
        CidV1.WriteCid(writeStream, originalCid);

        // Act - Read
        writeStream.Position = 0;
        var readCid = CidV1.ReadCid(writeStream);

        // Assert
        Assert.Equal(originalCid.Version.Value, readCid.Version.Value);
        Assert.Equal(originalCid.Multicodec.Value, readCid.Multicodec.Value);
        Assert.Equal(originalCid.HashFunction.Value, readCid.HashFunction.Value);
        Assert.Equal(originalCid.DigestSize.Value, readCid.DigestSize.Value);
        Assert.Equal(originalCid.DigestBytes, readCid.DigestBytes);
    }

    [Fact]
    public void ReadCid_ThenWriteCid_ProducesSameBytes()
    {
        // Arrange - Create valid CID bytes manually
        using var originalStream = new MemoryStream();
        originalStream.WriteByte(0x01); // version
        originalStream.WriteByte(0x71); // multicodec (dag-cbor)
        originalStream.WriteByte(0x12); // hash function (sha2-256)
        originalStream.WriteByte(0x20); // digest size (32)
        
        // Write 32 bytes of digest
        var digestBytes = new byte[32];
        for (int i = 0; i < 32; i++)
        {
            digestBytes[i] = (byte)i;
        }
        originalStream.Write(digestBytes, 0, 32);

        byte[] originalBytes = originalStream.ToArray();

        // Act - Read the CID
        originalStream.Position = 0;
        var cid = CidV1.ReadCid(originalStream);

        // Act - Write the CID to a new stream
        using var newStream = new MemoryStream();
        CidV1.WriteCid(newStream, cid);
        byte[] newBytes = newStream.ToArray();

        // Assert - The written bytes should match the original bytes
        Assert.Equal(originalBytes, newBytes);
    }

    [Fact]
    public void WriteCid_BytesMatchAllBytes()
    {
        // Arrange
        var cid = new CidV1
        {
            Version = new VarInt { Value = 1 },
            Multicodec = new VarInt { Value = 0x71 },
            HashFunction = new VarInt { Value = 0x12 },
            DigestSize = new VarInt { Value = 32 },
            DigestBytes = new byte[32],
            AllBytes = new byte[36],
            Base32 = ""
        };

        // Manually construct what AllBytes should be
        using var expectedStream = new MemoryStream();
        expectedStream.WriteByte(0x01); // version
        expectedStream.WriteByte(0x71); // multicodec
        expectedStream.WriteByte(0x12); // hash function
        expectedStream.WriteByte(0x20); // digest size
        expectedStream.Write(cid.DigestBytes, 0, 32);
        byte[] expectedBytes = expectedStream.ToArray();

        // Act
        using var actualStream = new MemoryStream();
        CidV1.WriteCid(actualStream, cid);
        byte[] actualBytes = actualStream.ToArray();

        // Assert
        Assert.Equal(expectedBytes, actualBytes);
    }

    [Fact]
    public void WriteAndReadCid_MultipleSequential_RoundTrip()
    {
        // Arrange - Create multiple CIDs
        var cids = new List<CidV1>
        {
            new CidV1
            {
                Version = new VarInt { Value = 1 },
                Multicodec = new VarInt { Value = 0x71 },
                HashFunction = new VarInt { Value = 0x12 },
                DigestSize = new VarInt { Value = 32 },
                DigestBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(),
                AllBytes = new byte[36],
                Base32 = ""
            },
            new CidV1
            {
                Version = new VarInt { Value = 1 },
                Multicodec = new VarInt { Value = 0x55 },
                HashFunction = new VarInt { Value = 0x12 },
                DigestSize = new VarInt { Value = 32 },
                DigestBytes = Enumerable.Range(0, 32).Select(i => (byte)(i * 2)).ToArray(),
                AllBytes = new byte[36],
                Base32 = ""
            }
        };

        using var stream = new MemoryStream();

        // Act - Write all CIDs
        foreach (var cid in cids)
        {
            CidV1.WriteCid(stream, cid);
        }

        // Act - Read all CIDs back
        stream.Position = 0;
        var readCids = new List<CidV1>();
        for (int i = 0; i < cids.Count; i++)
        {
            readCids.Add(CidV1.ReadCid(stream));
        }

        // Assert
        Assert.Equal(cids.Count, readCids.Count);
        for (int i = 0; i < cids.Count; i++)
        {
            Assert.Equal(cids[i].Version.Value, readCids[i].Version.Value);
            Assert.Equal(cids[i].Multicodec.Value, readCids[i].Multicodec.Value);
            Assert.Equal(cids[i].HashFunction.Value, readCids[i].HashFunction.Value);
            Assert.Equal(cids[i].DigestSize.Value, readCids[i].DigestSize.Value);
            Assert.Equal(cids[i].DigestBytes, readCids[i].DigestBytes);
        }
    }

    [Fact]
    public void ComputeCidForDagCbor_ToBytes_Deterministic()
    {
        // Arrange - Create a DagCborObject from JSON similar to a post
        string jsonString = @"{
            ""$type"": ""app.bsky.feed.post"",
            ""createdAt"": ""2026-01-12T08:53:11.276Z"",
            ""langs"": [""en""],
            ""text"": ""hey there another new post""
        }";
        
        var dagCbor = DagCborObject.FromJsonString(jsonString);
        
        // Act - Compute CID and get bytes multiple times
        var cid1 = CidV1.ComputeCidForDagCbor(dagCbor);
        var bytes1 = dagCbor.ToBytes();
        
        var cid2 = CidV1.ComputeCidForDagCbor(dagCbor);
        var bytes2 = dagCbor.ToBytes();
        
        // Assert - Should be identical
        Assert.Equal(cid1.Base32, cid2.Base32);
        Assert.Equal(bytes1, bytes2);
        
        // Verify CID matches hash of bytes
        var hash = System.Security.Cryptography.SHA256.HashData(bytes1);
        Assert.Equal(hash, cid1.DigestBytes);
    }
    
    [Fact]
    public void ComputeCidForDagCbor_CanonicalOrder()
    {
        // Arrange - Verify canonical key ordering (by length, then lexicographic)
        string jsonString = @"{
            ""$type"": ""app.bsky.feed.post"",
            ""createdAt"": ""2026-01-12T08:53:11.276Z"",
            ""langs"": [""en""],
            ""text"": ""hey there another new post""
        }";
        
        var dagCbor = DagCborObject.FromJsonString(jsonString);
        var bytes = dagCbor.ToBytes();
        
        // Parse back and verify order
        using var ms = new MemoryStream(bytes);
        var parsed = DagCborObject.ReadFromStream(ms);
        var dict = parsed.Value as Dictionary<string, DagCborObject>;
        
        // Keys should be in canonical order: text (4), $type (5), langs (5), createdAt (9)
        var keys = dict!.Keys.ToList();
        Assert.Equal(4, keys.Count);
        Assert.Equal("text", keys[0]);      // 4 bytes
        Assert.Equal("$type", keys[1]);     // 5 bytes  
        Assert.Equal("langs", keys[2]);     // 5 bytes (after $type lexicographically)
        Assert.Equal("createdAt", keys[3]); // 9 bytes
    }

    [Fact]
    public void InsertAndRetrieve_CidMatchesBytes()
    {
        // Simulate the exact flow in UserRepo.ApplyWrites and GetRepoRecordRawBytes
        
        // Step 1: Create DagCborObject from JSON (like ComAtprotoRepo_ApplyWrites does)
        string jsonString = @"{
            ""createdAt"": ""2026-01-12T08:53:11.276Z"",
            ""langs"": [""en""],
            ""text"": ""hey there another new post""
        }";
        var record = DagCborObject.FromJsonString(jsonString);
        
        // Step 2: Set $type (like UserRepo.ApplyWrites does)
        record.SetString(new string[] { "$type" }, "app.bsky.feed.post");
        
        // Step 3: Compute CID (like UserRepo.ApplyWrites does)
        CidV1 recordCid = CidV1.ComputeCidForDagCbor(record);
        
        // Step 4: Get bytes for storage (like InsertRepoRecord does)
        byte[] storedBytes = record.ToBytes();
        
        // Step 5: Simulate retrieval - get CID from base32 (like GetRepoRecordRawBytes does)
        CidV1 retrievedCid = CidV1.FromBase32(recordCid.Base32);
        
        // Step 6: Verify CID bytes match
        Assert.Equal(recordCid.AllBytes, retrievedCid.AllBytes);
        
        // Step 7: Verify hash of stored bytes matches CID
        var hash = System.Security.Cryptography.SHA256.HashData(storedBytes);
        Assert.Equal(hash, retrievedCid.DigestBytes);
        
        // Step 8: Verify ToBytes is deterministic
        byte[] bytesAgain = record.ToBytes();
        Assert.Equal(storedBytes, bytesAgain);
    }


    [Fact]
    public void ComputeCidForBlobBytes_Deterministic()
    {
        // Arrange - Create some blob bytes
        byte[] blobBytes = new byte[256];
        for (int i = 0; i < blobBytes.Length; i++)
        {
            blobBytes[i] = (byte)(i % 256);
        }

        // Act - Compute CID multiple times
        var cid1 = CidV1.ComputeCidForBlobBytes(blobBytes);
        var cid2 = CidV1.ComputeCidForBlobBytes(blobBytes);

        // Assert - CIDs should be identical
        Assert.Equal(cid1.Base32, cid2.Base32);

        // Verify CID matches hash of blob bytes
        var hash = System.Security.Cryptography.SHA256.HashData(blobBytes);
        Assert.Equal(hash, cid1.DigestBytes);
    }
}
