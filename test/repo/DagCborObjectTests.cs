namespace dnproto.tests.repo;

using dnproto.repo;
using System.Text;
using System.Text.Json;

public class DagCborObjectTests
{
    [Fact]
    public void RoundTrip_UnsignedInt_Small()
    {
        // Arrange - small value (< 24)
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_UNSIGNED_INT,
                AdditionalInfo = 15,
                OriginalByte = 0
            },
            Value = 15
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_UNSIGNED_INT, result.Type.MajorType);
        Assert.Equal(15, result.Value);
    }

    [Fact]
    public void RoundTrip_UnsignedInt_OneByte()
    {
        // Arrange - value requiring 1 byte (24-255)
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_UNSIGNED_INT,
                AdditionalInfo = 24,
                OriginalByte = 0
            },
            Value = 100
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_UNSIGNED_INT, result.Type.MajorType);
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void RoundTrip_UnsignedInt_TwoBytes()
    {
        // Arrange - value requiring 2 bytes (256-65535)
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_UNSIGNED_INT,
                AdditionalInfo = 25,
                OriginalByte = 0
            },
            Value = 1000
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_UNSIGNED_INT, result.Type.MajorType);
        Assert.Equal(1000, result.Value);
    }

    [Fact]
    public void RoundTrip_UnsignedInt_FourBytes()
    {
        // Arrange - value requiring 4 bytes (> 65535)
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_UNSIGNED_INT,
                AdditionalInfo = 26,
                OriginalByte = 0
            },
            Value = 100000
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_UNSIGNED_INT, result.Type.MajorType);
        Assert.Equal(100000, result.Value);
    }

    [Fact]
    public void RoundTrip_Text_ShortString()
    {
        // Arrange - short text string
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_TEXT,
                AdditionalInfo = 5,
                OriginalByte = 0
            },
            Value = "hello"
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_TEXT, result.Type.MajorType);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void RoundTrip_Text_LongString()
    {
        // Arrange - longer text string (> 23 chars)
        var longText = "This is a longer test string with more than 23 characters";
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_TEXT,
                AdditionalInfo = 24,
                OriginalByte = 0
            },
            Value = longText
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_TEXT, result.Type.MajorType);
        Assert.Equal(longText, result.Value);
    }

    [Fact]
    public void RoundTrip_Text_EmptyString()
    {
        // Arrange - empty string
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_TEXT,
                AdditionalInfo = 0,
                OriginalByte = 0
            },
            Value = ""
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_TEXT, result.Type.MajorType);
        Assert.Equal("", result.Value);
    }

    [Fact]
    public void RoundTrip_ByteString_Short()
    {
        // Arrange - short byte string
        var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_BYTE_STRING,
                AdditionalInfo = 5,
                OriginalByte = 0
            },
            Value = bytes
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_BYTE_STRING, result.Type.MajorType);
        Assert.Equal(bytes, (byte[])result.Value);
    }

    [Fact]
    public void RoundTrip_ByteString_Long()
    {
        // Arrange - longer byte string (> 23 bytes)
        var bytes = new byte[50];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(i % 256);
        }
        
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_BYTE_STRING,
                AdditionalInfo = 24,
                OriginalByte = 0
            },
            Value = bytes
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_BYTE_STRING, result.Type.MajorType);
        Assert.Equal(bytes, (byte[])result.Value);
    }

    [Fact]
    public void RoundTrip_SimpleValue_Null()
    {
        // Arrange - null value
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_SIMPLE_VALUE,
                AdditionalInfo = 0x16,
                OriginalByte = 0
            },
            Value = "null"
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_SIMPLE_VALUE, result.Type.MajorType);
        Assert.Equal("null", result.Value);
    }

    [Fact]
    public void RoundTrip_SimpleValue_True()
    {
        // Arrange - true value
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_SIMPLE_VALUE,
                AdditionalInfo = 0x15,
                OriginalByte = 0
            },
            Value = true
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_SIMPLE_VALUE, result.Type.MajorType);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public void RoundTrip_SimpleValue_False()
    {
        // Arrange - false value
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_SIMPLE_VALUE,
                AdditionalInfo = 0x14,
                OriginalByte = 0
            },
            Value = false
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_SIMPLE_VALUE, result.Type.MajorType);
        Assert.Equal(false, result.Value);
    }

    [Fact]
    public void RoundTrip_Array_Empty()
    {
        // Arrange - empty array
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_ARRAY,
                AdditionalInfo = 0,
                OriginalByte = 0
            },
            Value = new List<DagCborObject>()
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_ARRAY, result.Type.MajorType);
        var resultList = (List<DagCborObject>)result.Value;
        Assert.Empty(resultList);
    }

    [Fact]
    public void RoundTrip_Array_WithIntegers()
    {
        // Arrange - array with integers
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_ARRAY,
                AdditionalInfo = 3,
                OriginalByte = 0
            },
            Value = new List<DagCborObject>
            {
                new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 1, OriginalByte = 0 },
                    Value = 1
                },
                new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 2, OriginalByte = 0 },
                    Value = 2
                },
                new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 3, OriginalByte = 0 },
                    Value = 3
                }
            }
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_ARRAY, result.Type.MajorType);
        var resultList = (List<DagCborObject>)result.Value;
        Assert.Equal(3, resultList.Count);
        Assert.Equal(1, resultList[0].Value);
        Assert.Equal(2, resultList[1].Value);
        Assert.Equal(3, resultList[2].Value);
    }

    [Fact]
    public void RoundTrip_Array_WithMixedTypes()
    {
        // Arrange - array with mixed types
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_ARRAY,
                AdditionalInfo = 4,
                OriginalByte = 0
            },
            Value = new List<DagCborObject>
            {
                new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 42, OriginalByte = 0 },
                    Value = 42
                },
                new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 5, OriginalByte = 0 },
                    Value = "hello"
                },
                new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_SIMPLE_VALUE, AdditionalInfo = 0x15, OriginalByte = 0 },
                    Value = true
                },
                new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_SIMPLE_VALUE, AdditionalInfo = 0x14, OriginalByte = 0 },
                    Value = false
                }
            }
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_ARRAY, result.Type.MajorType);
        var resultList = (List<DagCborObject>)result.Value;
        Assert.Equal(4, resultList.Count);
        Assert.Equal(42, resultList[0].Value);
        Assert.Equal("hello", resultList[1].Value);
        Assert.Equal(true, resultList[2].Value);
        Assert.Equal(false, resultList[3].Value);
    }

    [Fact]
    public void RoundTrip_Map_Empty()
    {
        // Arrange - empty map
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_MAP,
                AdditionalInfo = 0,
                OriginalByte = 0
            },
            Value = new Dictionary<string, DagCborObject>()
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_MAP, result.Type.MajorType);
        var resultDict = (Dictionary<string, DagCborObject>)result.Value;
        Assert.Empty(resultDict);
    }

    [Fact]
    public void RoundTrip_Map_WithSimpleValues()
    {
        // Arrange - map with simple values
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_MAP,
                AdditionalInfo = 2,
                OriginalByte = 0
            },
            Value = new Dictionary<string, DagCborObject>
            {
                ["name"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 4, OriginalByte = 0 },
                    Value = "John"
                },
                ["age"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 24, OriginalByte = 0 },
                    Value = 30
                }
            }
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_MAP, result.Type.MajorType);
        var resultDict = (Dictionary<string, DagCborObject>)result.Value;
        Assert.Equal(2, resultDict.Count);
        Assert.True(resultDict.ContainsKey("name"));
        Assert.True(resultDict.ContainsKey("age"));
        Assert.Equal("John", resultDict["name"].Value);
        Assert.Equal(30, resultDict["age"].Value);
    }

    [Fact]
    public void RoundTrip_Map_Nested()
    {
        // Arrange - nested map
        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_MAP,
                AdditionalInfo = 2,
                OriginalByte = 0
            },
            Value = new Dictionary<string, DagCborObject>
            {
                ["user"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 2, OriginalByte = 0 },
                    Value = new Dictionary<string, DagCborObject>
                    {
                        ["name"] = new DagCborObject
                        {
                            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 5, OriginalByte = 0 },
                            Value = "Alice"
                        },
                        ["active"] = new DagCborObject
                        {
                            Type = new DagCborType { MajorType = DagCborType.TYPE_SIMPLE_VALUE, AdditionalInfo = 0x15, OriginalByte = 0 },
                            Value = true
                        }
                    }
                },
                ["count"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 10, OriginalByte = 0 },
                    Value = 10
                }
            }
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_MAP, result.Type.MajorType);
        var resultDict = (Dictionary<string, DagCborObject>)result.Value;
        Assert.Equal(2, resultDict.Count);
        
        var userDict = (Dictionary<string, DagCborObject>)resultDict["user"].Value;
        Assert.Equal("Alice", userDict["name"].Value);
        Assert.Equal(true, userDict["active"].Value);
        Assert.Equal(10, resultDict["count"].Value);
    }

    [Fact]
    public void RoundTrip_Tag_CidV1()
    {
        // Arrange - CID tag
        var cid = new CidV1
        {
            Version = new VarInt { Value = 1 },
            Multicodec = new VarInt { Value = 0x71 }, // dag-cbor
            HashFunction = new VarInt { Value = 0x12 }, // sha2-256
            DigestSize = new VarInt { Value = 32 },
            DigestBytes = new byte[32]
            {
                0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
                0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
                0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20
            },
            AllBytes = new byte[36],
            Base32 = ""
        };

        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_TAG,
                AdditionalInfo = 24,
                OriginalByte = 0
            },
            Value = cid
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_TAG, result.Type.MajorType);
        var resultCid = (CidV1)result.Value;
        Assert.Equal(cid.Version.Value, resultCid.Version.Value);
        Assert.Equal(cid.Multicodec.Value, resultCid.Multicodec.Value);
        Assert.Equal(cid.HashFunction.Value, resultCid.HashFunction.Value);
        Assert.Equal(cid.DigestSize.Value, resultCid.DigestSize.Value);
        Assert.Equal(cid.DigestBytes, resultCid.DigestBytes);
    }

    [Fact]
    public void RoundTrip_ComplexObject_PostRecord()
    {
        // Arrange - simulate a typical AT Protocol post record structure
        var cid = new CidV1
        {
            Version = new VarInt { Value = 1 },
            Multicodec = new VarInt { Value = 0x71 },
            HashFunction = new VarInt { Value = 0x12 },
            DigestSize = new VarInt { Value = 32 },
            DigestBytes = new byte[32]
            {
                0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
                0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
                0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20
            },
            AllBytes = new byte[36],
            Base32 = ""
        };

        var original = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_MAP,
                AdditionalInfo = 0,
                OriginalByte = 0
            },
            Value = new Dictionary<string, DagCborObject>
            {
                ["$type"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                    Value = "app.bsky.feed.post"
                },
                ["text"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                    Value = "Hello, ATProto!"
                },
                ["createdAt"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                    Value = "2023-01-01T12:00:00Z"
                },
                ["langs"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_ARRAY, AdditionalInfo = 1, OriginalByte = 0 },
                    Value = new List<DagCborObject>
                    {
                        new DagCborObject
                        {
                            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 2, OriginalByte = 0 },
                            Value = "en"
                        }
                    }
                },
                ["embed"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 2, OriginalByte = 0 },
                    Value = new Dictionary<string, DagCborObject>
                    {
                        ["$type"] = new DagCborObject
                        {
                            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                            Value = "app.bsky.embed.images"
                        },
                        ["images"] = new DagCborObject
                        {
                            Type = new DagCborType { MajorType = DagCborType.TYPE_ARRAY, AdditionalInfo = 1, OriginalByte = 0 },
                            Value = new List<DagCborObject>
                            {
                                new DagCborObject
                                {
                                    Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 2, OriginalByte = 0 },
                                    Value = new Dictionary<string, DagCborObject>
                                    {
                                        ["image"] = new DagCborObject
                                        {
                                            Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 24, OriginalByte = 0 },
                                            Value = cid
                                        },
                                        ["alt"] = new DagCborObject
                                        {
                                            Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT, AdditionalInfo = 0, OriginalByte = 0 },
                                            Value = "A test image"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        // Act
        using var stream = new MemoryStream();
        DagCborObject.WriteToStream(original, stream);
        stream.Position = 0;
        var result = DagCborObject.ReadFromStream(stream);

        // Assert
        Assert.Equal(DagCborType.TYPE_MAP, result.Type.MajorType);
        var resultDict = (Dictionary<string, DagCborObject>)result.Value;
        
        Assert.Equal("app.bsky.feed.post", resultDict["$type"].Value);
        Assert.Equal("Hello, ATProto!", resultDict["text"].Value);
        Assert.Equal("2023-01-01T12:00:00Z", resultDict["createdAt"].Value);
        
        var langs = (List<DagCborObject>)resultDict["langs"].Value;
        Assert.Single(langs);
        Assert.Equal("en", langs[0].Value);
        
        var embed = (Dictionary<string, DagCborObject>)resultDict["embed"].Value;
        Assert.Equal("app.bsky.embed.images", embed["$type"].Value);
        
        var images = (List<DagCborObject>)embed["images"].Value;
        Assert.Single(images);
        
        var imageObj = (Dictionary<string, DagCborObject>)images[0].Value;
        var imageCid = (CidV1)imageObj["image"].Value;
        Assert.Equal(cid.DigestBytes, imageCid.DigestBytes);
        Assert.Equal("A test image", imageObj["alt"].Value);
    }


    [Fact]
    public void ParseJsonToDagCborObject()
    {
        // Read test JSON file from the output directory
        string testDir = Path.GetDirectoryName(typeof(DagCborObjectTests).Assembly.Location)!;
        string jsonFilePath = Path.Combine(testDir, "repo", "post-payload-apply-writes.json");
        string jsonContent = File.ReadAllText(jsonFilePath);
        DagCborObject dagCborObject = DagCborObject.FromJsonString(jsonContent);

        Assert.Equal(DagCborType.TYPE_MAP, dagCborObject.Type.MajorType);

        Dictionary<string, object>? rawObject = (Dictionary<string, object>?)dagCborObject.GetRawValue();

        Assert.Equal("did:plc:l6fxvp2iu2h53auxadff3oyb", rawObject?["repo"]);

        List<object>? writes = rawObject?["writes"] as List<object>;

        Assert.NotNull(writes);
        Assert.Single(writes!);

        Dictionary<string, object>? firstWrite = writes![0] as Dictionary<string, object>;

        Assert.Equal("com.atproto.repo.applyWrites#create", firstWrite?["$type"]);
        
    }

    [Fact]
    public void SetString()
    {
        string initialJson = "{\"key\":\"value\"}";
        var dagCborObject = DagCborObject.FromJsonString(initialJson);
        dagCborObject.SetString(new string[] { "hello" }, "world");

        Assert.Equal(DagCborType.TYPE_MAP, dagCborObject.Type.MajorType);
        Assert.Equal("world", dagCborObject.SelectString(new string[] { "hello" }));
    }


    [Fact]
    public void Json_ToObject_ToDagCbor()
    {
        var json = """
            {
                "$type": "app.bsky.feed.post",
                "createdAt": "2026-02-04T02:52:51.219Z",
                "text": "more testing in prod",
                "embed": {
                    "$type": "app.bsky.embed.images",
                    "images": [
                        {
                            "image": {
                                "$type": "blob",
                                "ref": {
                                    "$link": "bafkreibbsbcpklag3blm5kba6jclb3sqeeoodqtsb7gh7343jsvy7343dy"
                                },
                                "mimeType": "image/jpeg",
                                "size": 160379
                            },
                            "alt": "",
                            "aspectRatio": {
                                "width": 871,
                                "height": 455
                            }
                        }
                    ]
                },
                "langs": [
                    "en"
                ]
            }
            """;

        // convert to object (this produces a JsonElement)
        object? o = JsonData.ConvertJsonStringToObject(json);

        // assert object type
        Assert.IsType<JsonElement>(o);
        Assert.Equal("JsonElement", o.GetType().Name);

        // convert to DagCborObject
        DagCborObject dagCborObject = DagCborObject.FromRawValue(o);

    }

}
