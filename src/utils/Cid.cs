
namespace dnproto.utils;

public class Cid
{
    // first byte
    public required VarInt Version { get; set; }

    // second byte
    public required VarInt Multicodec { get; set; }

    // third byte
    public required VarInt HashFunction { get; set; }

    // fourth byte
    public required VarInt DigestSize { get; set; }

    // next (and final) 32 bytes
    public required byte[] DigestBytes { get; set; }

    // entire array of bytes (including version, multicodec, hash function, digest size, and digest bytes)
    public required byte[] AllBytes { get; set; }

    // base32 representation of the CID
    public required string Base32 { get; set; }


    public static Cid ReadCid(Stream s)
    {
        // https://github.com/multiformats/cid
        VarInt version = VarInt.ReadVarInt(s);
        VarInt multicodec = VarInt.ReadVarInt(s);
        VarInt hashFunction = VarInt.ReadVarInt(s); // likely sha2-256, 0x12, decimal 18
        VarInt digestSize = VarInt.ReadVarInt(s);

        // https://github.com/multiformats/multicodec/blob/master/table.csv
        // dag-cbor = 0x71
        // raw = 0x55
        // should not happen for AT
        if(multicodec.Value != 0x71 && multicodec.Value != 0x55)
        {
            throw new Exception($"cidMulticodec.Value != 0x71 or 0x55: {multicodec.Value}");
        }

        byte[] digestBytes = new byte[digestSize.Value];
        int cidDigestBytesRead = s.Read(digestBytes, 0, digestSize.Value);

        var ms = new MemoryStream();
        ms.WriteByte((byte)version.Value);
        ms.WriteByte((byte)multicodec.Value);
        ms.WriteByte((byte)hashFunction.Value);
        ms.WriteByte((byte)digestSize.Value);
        ms.Write(digestBytes, 0, digestSize.Value);
        byte[] allBytes = ms.ToArray();

        string base32 = "b" + Base32Encoding.BytesToBase32(allBytes);

        return new Cid
        {
            Version = version,
            Multicodec = multicodec,
            HashFunction = hashFunction,
            DigestSize = digestSize,
            DigestBytes = digestBytes,
            AllBytes = allBytes,
            Base32 = base32
        };
    } 

    public byte[] GetBytes()
    {
        return AllBytes;
    }

    public string GetBase32()
    {
        return Base32;
    }

    public override string ToString()
    {
        return Base32;
    }
}