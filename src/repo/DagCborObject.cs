using System.Text;

namespace dnproto.repo;

/// <summary>
/// Represents the data block section of a repo record (after version and cid).
/// You normally start by reading a repo with "ReadFromStream". See Repo.WalkRepo for an example.
/// Then you can inspect the data with the Select* functions, which will return a value at a path.
/// </summary>
public class DagCborObject
{
    public required DagCborType Type;

    public required object Value;

    
    /// <summary>
    /// Read a DagCbor object from a stream. Recursively reads maps and arrays.
    /// The top-level type for a record's data block is *usually* a map, containing one or more elements.
    /// For example, a post record will have a map with keys like "text", "$type", "createdAt", etc.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static DagCborObject ReadFromStream(Stream s, Dictionary<string, DagCborObject>? dataBlockDict = null)
    {
        DagCborType type = DagCborType.ReadNextType(s);
        int length = 0;

        switch(type.MajorType)
        {
            case DagCborType.TYPE_MAP:
                length = ReadLengthFromStream(type, s); // might read one more byte for length
                Dictionary<string,DagCborObject> dict = dataBlockDict != null ? dataBlockDict : new Dictionary<string,DagCborObject>();

                for(int i = 0; i < length; i++)
                {
                    DagCborObject key = ReadFromStream(s);
                    string? keyString = key != null ? key.TryGetString() : null;
                    DagCborObject value = ReadFromStream(s);

                    if(keyString != null)
                    {
                        dict[keyString] = value;
                    }
                    else
                    {
                        throw new Exception("Key is null.");
                    }
                }

                return new DagCborObject { Type = type, Value = dict };

            case DagCborType.TYPE_ARRAY:
                length = ReadLengthFromStream(type, s);
                List<DagCborObject> list = new List<DagCborObject>();

                for(int i = 0; i < length; i++)
                {
                    var value = ReadFromStream(s);
                    list.Add(value);
                }

                return new DagCborObject { Type = type, Value = list };


            case DagCborType.TYPE_TEXT:
                length = ReadLengthFromStream(type, s);
                byte[] bytes = new byte[length];
                int readLength = s.Read(bytes, 0, length);
                return new DagCborObject { Type = type, Value = Encoding.UTF8.GetString(bytes) };

            case DagCborType.TYPE_TAG:
                int tag = s.ReadByte();
                if(tag != 42)
                {
                    throw new Exception("Unknown tag: " + tag);
                }

               DagCborType byteStringType =DagCborType.ReadNextType(s);
                length = ReadLengthFromStream(byteStringType, s);
                int shouldBeZero = s.ReadByte(); // read one more byte for 0

                CidV1 cid = CidV1.ReadCid(s);

                return new DagCborObject { Type = type, Value = cid };

            case DagCborType.TYPE_UNSIGNED_INT:
                return new DagCborObject { Type = type, Value = ReadLengthFromStream(type, s) };

            case DagCborType.TYPE_BYTE_STRING:
                length = ReadLengthFromStream(type, s);
                byte[] byteString = new byte[length];
                int bytesRead = s.Read(byteString, 0, length);
                return new DagCborObject { Type = type, Value = byteString };

            case DagCborType.TYPE_SIMPLE_VALUE:
                if(type.AdditionalInfo == 0x16)
                {
                    return new DagCborObject { Type = type, Value = "null" };
                }
                else if(type.AdditionalInfo == 0x14)
                {
                    return new DagCborObject { Type = type, Value = false };
                }
                else if(type.AdditionalInfo == 0x15)
                {
                    return new DagCborObject { Type = type, Value = true };
                }
                else
                {
                    throw new Exception("Unknown simple value: " + type.AdditionalInfo);
                }

            default:
                throw new Exception("Unknown major type: " + type.MajorType);
        }
    }

    public void WriteToStream(Stream s)
    {
        WriteToStream(this, s);
    }
    
    /// <summary>
    /// Write a DagCbor object to a stream. Recursively writes maps and arrays.
    /// This is the mirror operation to ReadFromStream.
    /// </summary>
    /// <param name="obj">The DagCborObject to write</param>
    /// <param name="s">The stream to write to</param>
    /// <exception cref="Exception"></exception>
    public static void WriteToStream(DagCborObject obj, Stream s)
    {
        switch(obj.Type.MajorType)
        {
            case DagCborType.TYPE_MAP:
                Dictionary<string, DagCborObject>? dict = obj.Value as Dictionary<string, DagCborObject>;
                if(dict == null)
                {
                    throw new Exception("Expected Dictionary for TYPE_MAP");
                }
                
                WriteLengthToStream(DagCborType.TYPE_MAP, dict.Count, s);
                
                // DAG-CBOR requires map keys to be sorted in canonical order:
                // first by byte length, then lexicographically by bytes
                var sortedKeys = dict.Keys.OrderBy(k => System.Text.Encoding.UTF8.GetByteCount(k))
                                          .ThenBy(k => k, StringComparer.Ordinal)
                                          .ToList();
                
                foreach(var key in sortedKeys)
                {
                    // Write key as text string
                    DagCborObject keyObj = new DagCborObject 
                    { 
                        Type = new DagCborType 
                        { 
                            MajorType = DagCborType.TYPE_TEXT, 
                            AdditionalInfo = key.Length,
                            OriginalByte = 0
                        }, 
                        Value = key 
                    };
                    WriteToStream(keyObj, s);
                    
                    // Write value
                    WriteToStream(dict[key], s);
                }
                break;

            case DagCborType.TYPE_ARRAY:
                List<DagCborObject>? list = obj.Value as List<DagCborObject>;
                if(list == null)
                {
                    throw new Exception("Expected List for TYPE_ARRAY");
                }
                
                WriteLengthToStream(DagCborType.TYPE_ARRAY, list.Count, s);
                
                foreach(var item in list)
                {
                    WriteToStream(item, s);
                }
                break;

            case DagCborType.TYPE_TEXT:
                string? text = obj.Value as string;
                if(text == null)
                {
                    throw new Exception("Expected string for TYPE_TEXT");
                }
                
                byte[] textBytes = Encoding.UTF8.GetBytes(text);
                WriteLengthToStream(DagCborType.TYPE_TEXT, textBytes.Length, s);
                s.Write(textBytes, 0, textBytes.Length);
                break;

            case DagCborType.TYPE_TAG:
                // Write tag type and tag number (42 for CID)
                byte tagByte = (byte)((DagCborType.TYPE_TAG << 5) | 24); // tag with 1-byte uint
                s.WriteByte(tagByte);
                s.WriteByte(42); // CID tag
                
                CidV1? cid = obj.Value as CidV1;
                if(cid == null)
                {
                    throw new Exception("Expected CidV1 for TYPE_TAG");
                }
                
                // Calculate the total length needed for the byte string
                // This includes the version, multicodec, hash function, digest size, and digest bytes
                // Plus one byte for the multibase prefix (0)
                using (var tempStream = new MemoryStream())
                {
                    CidV1.WriteCid(tempStream, cid);
                    byte[] cidBytes = tempStream.ToArray();
                    
                    // Write byte string type for CID (with 0 prefix)
                    WriteLengthToStream(DagCborType.TYPE_BYTE_STRING, cidBytes.Length + 1, s);
                    s.WriteByte(0); // multibase prefix
                    s.Write(cidBytes, 0, cidBytes.Length);
                }
                break;

            case DagCborType.TYPE_UNSIGNED_INT:
                int? intValue = obj.Value as int?;
                if(intValue == null)
                {
                    throw new Exception("Expected int for TYPE_UNSIGNED_INT");
                }
                
                WriteLengthToStream(DagCborType.TYPE_UNSIGNED_INT, intValue.Value, s);
                break;

            case DagCborType.TYPE_BYTE_STRING:
                byte[]? byteString = obj.Value as byte[];
                if(byteString == null)
                {
                    throw new Exception("Expected byte[] for TYPE_BYTE_STRING");
                }
                
                WriteLengthToStream(DagCborType.TYPE_BYTE_STRING, byteString.Length, s);
                s.Write(byteString, 0, byteString.Length);
                break;

            case DagCborType.TYPE_SIMPLE_VALUE:
                byte simpleByte;
                if(obj.Value as string == "null")
                {
                    simpleByte = (byte)((DagCborType.TYPE_SIMPLE_VALUE << 5) | 0x16);
                }
                else if(obj.Value is bool boolVal)
                {
                    if(boolVal)
                    {
                        simpleByte = (byte)((DagCborType.TYPE_SIMPLE_VALUE << 5) | 0x15);
                    }
                    else
                    {
                        simpleByte = (byte)((DagCborType.TYPE_SIMPLE_VALUE << 5) | 0x14);
                    }
                }
                else
                {
                    throw new Exception("Unknown simple value: " + obj.Value);
                }
                s.WriteByte(simpleByte);
                break;

            default:
                throw new Exception("Unknown major type: " + obj.Type.MajorType);
        }
    }

    /// <summary>
    /// Write the length value to the stream with appropriate encoding.
    /// </summary>
    /// <param name="majorType">The CBOR major type</param>
    /// <param name="length">The length or value to encode</param>
    /// <param name="s">The stream to write to</param>
    private static void WriteLengthToStream(int majorType, int length, Stream s)
    {
        byte firstByte;
        
        if(length < 24)
        {
            firstByte = (byte)((majorType << 5) | length);
            s.WriteByte(firstByte);
        }
        else if(length < 256)
        {
            firstByte = (byte)((majorType << 5) | 24);
            s.WriteByte(firstByte);
            s.WriteByte((byte)length);
        }
        else if(length < 65536)
        {
            firstByte = (byte)((majorType << 5) | 25);
            s.WriteByte(firstByte);
            s.WriteByte((byte)(length >> 8));
            s.WriteByte((byte)(length & 0xFF));
        }
        else
        {
            firstByte = (byte)((majorType << 5) | 26);
            s.WriteByte(firstByte);
            s.WriteByte((byte)(length >> 24));
            s.WriteByte((byte)((length >> 16) & 0xFF));
            s.WriteByte((byte)((length >> 8) & 0xFF));
            s.WriteByte((byte)(length & 0xFF));
        }
    }

    public static void WriteToRepoStream(System.IO.Stream stream, CidV1 cid, DagCborObject dagCbor)
    {
        var cidBytes = cid.AllBytes;
        var dagCborBytes = dagCbor.ToBytes();
        var blockLengthVarInt = VarInt.FromLong((long)(cidBytes.Length + dagCborBytes.Length));

        VarInt.WriteVarInt(stream, blockLengthVarInt);
        CidV1.WriteCid(stream, cid);
        stream.Write(dagCborBytes, 0, dagCborBytes.Length);
    }



    /// <summary>
    /// Read the next length value from the stream.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="s"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private static int ReadLengthFromStream(DagCborType type, Stream s)
    {
        int length = 0;
        
        if(type.AdditionalInfo < 24)
        {
            length = type.AdditionalInfo;
        }
        else if(type.AdditionalInfo == 24)
        {
            length = (byte)s.ReadByte();
        }
        else if(type.AdditionalInfo == 25)
        {
            length = ((byte)s.ReadByte() << 8) | (byte)s.ReadByte();
        }
        else if (type.AdditionalInfo == 26)
        {
            length = ((byte)s.ReadByte() << 24) | ((byte)s.ReadByte() << 16) | ((byte)s.ReadByte() << 8) | (byte)s.ReadByte();
        }
        else
        {
            throw new Exception("Unknown additional info: " + type.AdditionalInfo);
        }
        
        return length;
    }


    /// <summary>
    /// Finds an object at the path specified by the property names,
    /// and returns its value.
    /// </summary>
    /// <param name="propertyNames"></param>
    /// <returns></returns>
    public object? SelectObjectValue(string[] propertyNames)
    {
        DagCborObject? current = this;

        foreach(string propertyName in propertyNames)
        {
            if(current.Type.MajorType != DagCborType.TYPE_MAP) return null;

            Dictionary<string,DagCborObject>? dict = current.Value as Dictionary<string,DagCborObject>;

            if(dict == null) return null;

            if(dict.ContainsKey(propertyName)) current = dict[propertyName];
            else return null;
        }

        return current != null ? current.Value : null;
    }

    public DagCborObject? SelectObject(string[] propertyNames)
    {
        DagCborObject? current = this;

        foreach(string propertyName in propertyNames)
        {
            if(current.Type.MajorType != DagCborType.TYPE_MAP) return null;

            Dictionary<string,DagCborObject>? dict = current.Value as Dictionary<string,DagCborObject>;

            if(dict == null) return null;

            if(dict.ContainsKey(propertyName)) current = dict[propertyName];
            else return null;
        }

        return current;
    }

    /// <summary>
    /// Finds an object at the path specified by the property names, 
    /// and returns as string if possible.
    /// </summary>
    /// <param name="propertyNames"></param>
    /// <returns></returns>
    public string? SelectString(string[] propertyNames)
    {
        object? o = SelectObjectValue(propertyNames);

        if(o as string != null) return o as string;
        if(o as int? != null) return o.ToString();
        if(o as bool? != null) return o.ToString();
        if(o as CidV1 != null) return ((CidV1)o).GetBase32();

        return null;
    }

    /// <summary>
    /// Finds an object at the path specified by the property names, 
    /// and returns as int if possible.
    /// </summary>
    /// <param name="propertyNames"></param>
    /// <returns></returns>
    public int? SelectInt(string[] propertyNames)
    {
        object? o = SelectObjectValue(propertyNames);

        if(o is int i) return i;

        return null;
    }

    public long? SelectLong(string[] propertyNames)
    {
        object? o = SelectObjectValue(propertyNames);

        if(o is long l) return l;
        if(o is int i) return (long)i;

        return null;
    }


    public bool SetString(string[] propertyNames, string strValue)
    {
        DagCborObject? current = this;

        for(int i = 0; i < propertyNames.Length; i++)
        {
            string propertyName = propertyNames[i];

            if(current.Type.MajorType != DagCborType.TYPE_MAP) return false;

            Dictionary<string,DagCborObject>? dict = current.Value as Dictionary<string,DagCborObject>;

            if(dict == null) return false;

            if(i == propertyNames.Length - 1)
            {
                // Last property, set value
                dict[propertyName] = new DagCborObject
                {
                    Type = new DagCborType
                    {
                        MajorType = DagCborType.TYPE_TEXT,
                        AdditionalInfo = 0,
                        OriginalByte = 0
                    },
                    Value = strValue
                };
            }
            else
            {
                // Intermediate property, navigate or create
                if(dict.ContainsKey(propertyName))
                {
                    current = dict[propertyName];
                }
                else
                {
                    DagCborObject newObj = new DagCborObject
                    {
                        Type = new DagCborType
                        {
                            MajorType = DagCborType.TYPE_MAP,
                            AdditionalInfo = 0,
                            OriginalByte = 0
                        },
                        Value = new Dictionary<string,DagCborObject>()
                    };
                    dict[propertyName] = newObj;
                    current = newObj;
                }
            }
        }

        return true;
    }



    public override string ToString()
    {
        return $"CborObject -> {TryGetString()}";
    }

    public string TryGetString()
    {
        string? sval = Value.ToString();
        return sval != null ? sval : "";
    }

    /// <summary>
    /// This extracts the data (Value) from this DagCborObject and its children.
    /// Helpful when you want to convert to something like json for debugging.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public object? GetRawValue()
    {
        if(Type.MajorType ==DagCborType.TYPE_TEXT)
        {
            return Value;
        }
        else if(Type.MajorType ==DagCborType.TYPE_BYTE_STRING)
        {
            return Value;
        }
        else if(Type.MajorType ==DagCborType.TYPE_UNSIGNED_INT)
        {
            return Value;
        }
        else if(Type.MajorType ==DagCborType.TYPE_SIMPLE_VALUE)
        {
            return Value;
        }
        else if(Type.MajorType ==DagCborType.TYPE_ARRAY)
        {
            List<object> list = new List<object>();

            foreach(var obj in (List<DagCborObject>)Value)
            {
                var v = obj.GetRawValue();
                if (v != null)
                {
                    list.Add(v);
                }
            }

            return list;
        }
        else if(Type.MajorType ==DagCborType.TYPE_MAP)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            foreach(KeyValuePair<string,DagCborObject> kvp in (Dictionary<string,DagCborObject>)Value)
            {
                var v = kvp.Value.GetRawValue();
                if(v != null)
                {
                    dict[kvp.Key] = v;
                }
            }
            return dict;
        }
        else if(Type.MajorType ==DagCborType.TYPE_TAG)
        {
            if(Value is CidV1)
            {
                return ((CidV1)Value).GetBase32();
            }
            else
            {
                return Value;
            }
        }
        else
        {
            throw new Exception("Unknown major type: " + Type.MajorType);
        }
    }

    public static DagCborObject FromException(Exception e, byte[] buffer, Dictionary<string, DagCborObject> dataBlockDict)
    {
        string bufferString = Encoding.UTF8.GetString(buffer);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Buffer String: " + bufferString);
        sb.AppendLine("Exception Type: " + e.GetType().Name);
        sb.AppendLine("Exception Message: " + e.Message);
        sb.AppendLine("Exception StackTrace: " + e.StackTrace);

        dataBlockDict["DNPROTO_EXCEPTION"] = new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_TEXT,
                AdditionalInfo = 0,
                OriginalByte = 0
            },
            Value = sb.ToString()
        };


        return new DagCborObject
        {
            Type = new DagCborType
            {
                MajorType = DagCborType.TYPE_MAP,
                AdditionalInfo = 0,
                OriginalByte = 0
            },
            Value = dataBlockDict
        };
    }

    public byte[] ToBytes()
    {
        using(MemoryStream ms = new MemoryStream())
        {
            DagCborObject.WriteToStream(this, ms);
            return ms.ToArray();
        }
    }

    public static DagCborObject FromBytes(byte[] data)
    {
        using(MemoryStream ms = new MemoryStream(data))
        {
            return DagCborObject.ReadFromStream(ms);
        }
    }


    /// <summary>
    /// Parses a JSON string into a DagCborObject.
    /// </summary>
    /// <param name="jsonString"></param>
    /// <returns></returns>
    public static DagCborObject FromJsonString(string jsonString)
    {
        object? o = JsonData.ConvertJsonStringToObject(jsonString);
        DagCborObject dagCborObject = DagCborObject.FromRawValue(o);
        return dagCborObject;
    }

    /// <summary>
    /// Creates a DagCborObject from a raw value (useful for converting from JSON).
    /// This is the inverse of GetRawValue().
    /// </summary>
    /// <param name="value">The raw value to convert</param>
    /// <returns>A DagCborObject representing the value</returns>
    /// <exception cref="Exception"></exception>
    public static DagCborObject FromRawValue(object? value)
    {
        if(value == null)
        {
            return new DagCborObject
            {
                Type = new DagCborType
                {
                    MajorType = DagCborType.TYPE_SIMPLE_VALUE,
                    AdditionalInfo = 0x16,
                    OriginalByte = (byte)((DagCborType.TYPE_SIMPLE_VALUE << 5) | 0x16)
                },
                Value = "null"
            };
        }
        else if(value is bool boolValue)
        {
            byte additionalInfo = boolValue ? (byte)0x15 : (byte)0x14;
            return new DagCborObject
            {
                Type = new DagCborType
                {
                    MajorType = DagCborType.TYPE_SIMPLE_VALUE,
                    AdditionalInfo = additionalInfo,
                    OriginalByte = (byte)((DagCborType.TYPE_SIMPLE_VALUE << 5) | additionalInfo)
                },
                Value = boolValue
            };
        }
        else if(value is int intValue)
        {
            return new DagCborObject
            {
                Type = new DagCborType
                {
                    MajorType = DagCborType.TYPE_UNSIGNED_INT,
                    AdditionalInfo = 0,
                    OriginalByte = 0
                },
                Value = intValue
            };
        }
        else if(value is long longValue)
        {
            return new DagCborObject
            {
                Type = new DagCborType
                {
                    MajorType = DagCborType.TYPE_UNSIGNED_INT,
                    AdditionalInfo = 0,
                    OriginalByte = 0
                },
                Value = (int)longValue
            };
        }
        else if(value is string stringValue)
        {
            // Don't auto-convert CID strings to Tag 42
            // CID strings should remain as text unless explicitly using {"$link": "..."} notation
            return new DagCborObject
            {
                Type = new DagCborType
                {
                    MajorType = DagCborType.TYPE_TEXT,
                    AdditionalInfo = 0,
                    OriginalByte = 0
                },
                Value = stringValue
            };
        }
        else if(value is byte[] byteArray)
        {
            return new DagCborObject
            {
                Type = new DagCborType
                {
                    MajorType = DagCborType.TYPE_BYTE_STRING,
                    AdditionalInfo = 0,
                    OriginalByte = 0
                },
                Value = byteArray
            };
        }
        else if(value is System.Text.Json.JsonElement jsonElement)
        {
            return FromJsonElement(jsonElement);
        }
        else if(value is List<object> list)
        {
            List<DagCborObject> cborList = new List<DagCborObject>();
            foreach(var item in list)
            {
                cborList.Add(FromRawValue(item));
            }

            return new DagCborObject
            {
                Type = new DagCborType
                {
                    MajorType = DagCborType.TYPE_ARRAY,
                    AdditionalInfo = 0,
                    OriginalByte = 0
                },
                Value = cborList
            };
        }
        else if(value is Dictionary<string, object> dict)
        {
            // Check if this is a CID link: {"$link": "bafyrei..."}
            if(dict.Count == 1 && dict.ContainsKey("$link") && dict["$link"] is string linkValue)
            {
                try
                {
                    CidV1 cid = CidV1.FromBase32(linkValue);
                    return new DagCborObject
                    {
                        Type = new DagCborType
                        {
                            MajorType = DagCborType.TYPE_TAG,
                            AdditionalInfo = 24,
                            OriginalByte = 0
                        },
                        Value = cid
                    };
                }
                catch
                {
                    // Not a valid CID, treat as regular dict
                }
            }

            Dictionary<string, DagCborObject> cborDict = new Dictionary<string, DagCborObject>();
            foreach(var kvp in dict)
            {
                cborDict[kvp.Key] = FromRawValue(kvp.Value);
            }

            return new DagCborObject
            {
                Type = new DagCborType
                {
                    MajorType = DagCborType.TYPE_MAP,
                    AdditionalInfo = 0,
                    OriginalByte = 0
                },
                Value = cborDict
            };
        }
        else
        {
            throw new Exception($"Cannot convert type {value.GetType().Name} to DagCborObject");
        }
    }

    /// <summary>
    /// Helper method to convert System.Text.Json.JsonElement to DagCborObject.
    /// </summary>
    private static DagCborObject FromJsonElement(System.Text.Json.JsonElement element)
    {
        switch(element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Null:
                return FromRawValue(null);
            
            case System.Text.Json.JsonValueKind.True:
                return FromRawValue(true);
            
            case System.Text.Json.JsonValueKind.False:
                return FromRawValue(false);
            
            case System.Text.Json.JsonValueKind.Number:
                if(element.TryGetInt32(out int intVal))
                    return FromRawValue(intVal);
                if(element.TryGetInt64(out long longVal))
                    return FromRawValue((int)longVal);
                throw new Exception("Number too large for int32");
            
            case System.Text.Json.JsonValueKind.String:
                return FromRawValue(element.GetString());
            
            case System.Text.Json.JsonValueKind.Array:
                List<object> list = new List<object>();
                foreach(var item in element.EnumerateArray())
                {
                    var rawValue = FromJsonElement(item).GetRawValue();
                    if(rawValue != null)
                        list.Add(rawValue);
                }
                return FromRawValue(list);
            
            case System.Text.Json.JsonValueKind.Object:
                Dictionary<string, object> dict = new Dictionary<string, object>();
                foreach(var prop in element.EnumerateObject())
                {
                    var rawValue = FromJsonElement(prop.Value).GetRawValue();
                    if(rawValue != null)
                        dict[prop.Name] = rawValue;
                }
                return FromRawValue(dict);
            
            default:
                throw new Exception($"Unsupported JsonValueKind: {element.ValueKind}");
        }
    }


    public static string GetRecursiveDebugString(DagCborObject obj, int indent = 0)
    {
        string indentStr = new string(' ', indent * 2);
        string result = $"{indentStr}Type: {obj.Type.GetMajorTypeString()}, Value: {obj.Value}\n";

        if (obj.Type.MajorType == DagCborType.TYPE_MAP && obj.Value is Dictionary<string, DagCborObject> dict)
        {
            foreach (var kvp in dict)
            {
                result += $"{indentStr}Key: {kvp.Key}\n";
                result += GetRecursiveDebugString(kvp.Value, indent + 1);
            }
        }
        else if (obj.Type.MajorType == DagCborType.TYPE_ARRAY && obj.Value is List<DagCborObject> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                result += $"{indentStr}Index: {i}\n";
                result += GetRecursiveDebugString(list[i], indent + 1);
            }
        }

        return result;
    }
}

public class DagCborType
{
    public int MajorType;
    public int AdditionalInfo;
    public byte OriginalByte;
    
    public static DagCborType ReadNextType(Stream s)
    {
        byte b = (byte)s.ReadByte();
        
        int majorType = b >> 5;
        int additionalInfo = b & 0x1F;
        
        return new DagCborType() { MajorType = majorType, AdditionalInfo = additionalInfo, OriginalByte = b };
    }

    public override string ToString()
    {
        return $"CborType -> {GetMajorTypeString()} ({MajorType}), AdditionalInfo: {AdditionalInfo}";
    }


    public string GetMajorTypeString()
    {
        switch(MajorType)
        {
            case TYPE_UNSIGNED_INT:
                return "TYPE_UNSIGNED_INT";
            case TYPE_NEGATIVE_INT:
                return "TYPE_NEGATIVE_INT";
            case TYPE_BYTE_STRING:
                return "TYPE_BYTE_STRING";
            case TYPE_TEXT:
                return "TYPE_TEXT";
            case TYPE_ARRAY:
                return "TYPE_ARRAY";
            case TYPE_MAP:
                return "TYPE_MAP";
            case TYPE_TAG:
                return "TYPE_TAG";
            case TYPE_SIMPLE_VALUE:
                return "TYPE_SIMPLE_VALUE";
            default:
                return "UNKNOWN";
        }
    }

    public const int TYPE_UNSIGNED_INT = 0;
    public const int TYPE_NEGATIVE_INT = 1;
    public const int TYPE_BYTE_STRING = 2;
    public const int TYPE_TEXT = 3;
    public const int TYPE_ARRAY = 4;
    public const int TYPE_MAP = 5;
    public const int TYPE_TAG = 6;
    public const int TYPE_SIMPLE_VALUE = 7;
}
