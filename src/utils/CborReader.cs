using System.Text;

namespace dnproto.utils;

public class CborReader
{
    /// <summary>
    /// Read a CBOR object from a stream. Recursively reads maps and arrays.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static CborObject ReadNext(Stream s)
    {
        CborType type = CborType.ReadNextType(s);
        int length = 0;

        switch(type.MajorType)
        {
            case 5: // TYPE_MAP
                length = GetLength(type, s); // might read one more byte for length
                Dictionary<string, object> dict = new Dictionary<string, object>();

                for(int i = 0; i < length; i++)
                {
                    CborObject key = ReadNext(s);
                    string? keyString = key != null ? key.AsDictionaryKey() : null;
                    CborObject value = ReadNext(s);

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
            case 3: // TYPE_TEXT
                length = GetLength(type, s);
                byte[] bytes = new byte[length];
                int readLength = s.Read(bytes, 0, length);
                return new CborObject { Type = type, Value = Encoding.UTF8.GetString(bytes) };
            case 4: // TYPE_ARRAY
                length = GetLength(type, s);
                List<object> list = new List<object>();

                for(int i = 0; i < length; i++)
                {
                    object value = ReadNext(s);
                    list.Add(value);
                }

                return new CborObject { Type = type, Value = list };
            case 6: // TYPE_TAG
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
            case 0: // TYPE_UNSIGNED_INT
                return new CborObject { Type = type, Value = GetLength(type, s) };
            case 2: // TYPE_BYTE_STRING
                length = GetLength(type, s);
                byte[] byteString = new byte[length];
                int bytesRead = s.Read(byteString, 0, length);
                return new CborObject { Type = type, Value = Encoding.UTF8.GetString(byteString) };
            case 7: // TYPE_SIMPLE_VALUE
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
        return $"{GetMajorTypeString()} ({MajorType}), AdditionalInfo: {AdditionalInfo}";
    }

    public string GetMajorTypeString()
    {
        switch(MajorType)
        {
            case 0:
                return "TYPE_UNSIGNED_INT";
            case 1:
                return "TYPE_NEGATIVE_INT";
            case 2:
                return "TYPE_BYTE_STRING";
            case 3:
                return "TYPE_TEXT";
            case 4:
                return "TYPE_ARRAY";
            case 5:
                return "TYPE_MAP";
            case 6:
                return "TYPE_TAG";
            case 7:
                return "TYPE_SIMPLE_VALUE";
            default:
                return "UNKNOWN";
        }
    }

}

public class CborObject
{
    public required CborType Type;

    public required object Value;

    public override string ToString()
    {
        return $"CborObject -> {AsDictionaryKey()}";
    }

    public string AsDictionaryKey()
    {
        var s = Value.ToString();
        return s != null ? s : "";
    }

}