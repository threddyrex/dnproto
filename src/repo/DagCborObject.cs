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
                return new DagCborObject { Type = type, Value = Encoding.UTF8.GetString(byteString) };

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
    /// Finds an object at the path specified by the property names.
    /// </summary>
    /// <param name="propertyNames"></param>
    /// <returns></returns>
    public object? SelectObject(string[] propertyNames)
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

    /// <summary>
    /// Finds an object at the path specified by the property names, 
    /// and returns as string if possible.
    /// </summary>
    /// <param name="propertyNames"></param>
    /// <returns></returns>
    public string? SelectString(string[] propertyNames)
    {
        object? o = SelectObject(propertyNames);

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
        object? o = SelectObject(propertyNames);

        if(o as int? != null) return o as int?;

        return null;
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
