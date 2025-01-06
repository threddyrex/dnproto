using System.Text;

namespace dnproto.utils;

public class CborObject
{
    public required CborType Type;

    public required object Value;

    /// <summary>
    /// Read a CBOR object from a stream. Recursively reads maps and arrays.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static CborObject ReadFromStream(Stream s)
    {
        CborType type = CborType.ReadNextType(s);
        int length = 0;

        switch(type.MajorType)
        {
            case CborType.TYPE_MAP:
                length = GetLength(type, s); // might read one more byte for length
                Dictionary<string, CborObject> dict = new Dictionary<string, CborObject>();

                for(int i = 0; i < length; i++)
                {
                    CborObject key = ReadFromStream(s);
                    string? keyString = key != null ? key.TryGetString() : null;
                    CborObject value = ReadFromStream(s);

                    if(keyString != null)
                    {
                        dict[keyString] = value;
                    }
                    else
                    {
                        throw new Exception("Key is null.");
                    }
                }

                return new CborObject { Type = type, Value = dict };

            case CborType.TYPE_ARRAY:
                length = GetLength(type, s);
                List<CborObject> list = new List<CborObject>();

                for(int i = 0; i < length; i++)
                {
                    var value = ReadFromStream(s);
                    list.Add(value);
                }

                return new CborObject { Type = type, Value = list };


            case CborType.TYPE_TEXT:
                length = GetLength(type, s);
                byte[] bytes = new byte[length];
                int readLength = s.Read(bytes, 0, length);
                return new CborObject { Type = type, Value = Encoding.UTF8.GetString(bytes) };

            case CborType.TYPE_TAG:
                int tag = s.ReadByte();
                if(tag != 42)
                {
                    throw new Exception("Unknown tag: " + tag);
                }

                CborType byteStringType = CborType.ReadNextType(s);
                length = GetLength(byteStringType, s);
                int shouldBeZero = s.ReadByte(); // read one more byte for 0

                Cid cid = Cid.ReadCid(s);

                return new CborObject { Type = type, Value = cid };

            case CborType.TYPE_UNSIGNED_INT:
                return new CborObject { Type = type, Value = GetLength(type, s) };

            case CborType.TYPE_BYTE_STRING:
                length = GetLength(type, s);
                byte[] byteString = new byte[length];
                int bytesRead = s.Read(byteString, 0, length);
                return new CborObject { Type = type, Value = Encoding.UTF8.GetString(byteString) };

            case CborType.TYPE_SIMPLE_VALUE:
                if(type.AdditionalInfo == 0x16)
                {
                    return new CborObject { Type = type, Value = "null" };
                }
                else if(type.AdditionalInfo == 0x14)
                {
                    s.ReadByte();
                    return new CborObject { Type = type, Value = false };
                }
                else if(type.AdditionalInfo == 0x15)
                {
                    s.ReadByte();
                    return new CborObject { Type = type, Value = true };
                }
                else
                {
                    throw new Exception("Unknown simple value: " + type.AdditionalInfo);
                }

            default:
                throw new Exception("Unknown major type: " + type.MajorType);
        }
    }

    public static int GetLength(CborType type, Stream s)
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
        else
        {
            throw new Exception("Unknown additional info: " + type.AdditionalInfo);
        }
        
        return length;
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

    public object? GetRawValue()
    {
        if(Type.MajorType == CborType.TYPE_TEXT)
        {
            return Value;
        }
        else if(Type.MajorType == CborType.TYPE_BYTE_STRING)
        {
            return Value;
        }
        else if(Type.MajorType == CborType.TYPE_UNSIGNED_INT)
        {
            return Value;
        }
        else if(Type.MajorType == CborType.TYPE_SIMPLE_VALUE)
        {
            return Value;
        }
        else if(Type.MajorType == CborType.TYPE_ARRAY)
        {
            List<object> list = new List<object>();

            foreach(var obj in (List<CborObject>)Value)
            {
                var v = obj.GetRawValue();
                if (v != null)
                {
                    list.Add(v);
                }
            }

            return list;
        }
        else if(Type.MajorType == CborType.TYPE_MAP)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            foreach(KeyValuePair<string, CborObject> kvp in (Dictionary<string, CborObject>)Value)
            {
                var v = kvp.Value.GetRawValue();
                if(v != null)
                {
                    dict[kvp.Key] = v;
                }
            }
            return dict;
        }
        else if(Type.MajorType == CborType.TYPE_TAG)
        {
            if(Value is Cid)
            {
                return ((Cid)Value).GetBase32();
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
}

public class CborType
{
    public int MajorType;
    public int AdditionalInfo;
    public byte OriginalByte;
    
    public static CborType ReadNextType(Stream s)
    {
        byte b = (byte)s.ReadByte();
        
        int majorType = b >> 5;
        int additionalInfo = b & 0x1F;
        
        return new CborType() { MajorType = majorType, AdditionalInfo = additionalInfo, OriginalByte = b };
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
