
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

    // next 32 bytes
    public required byte[] DigestBytes { get; set; }

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

        return new Cid
        {
            Version = version,
            Multicodec = multicodec,
            HashFunction = hashFunction,
            DigestSize = digestSize,
            DigestBytes = digestBytes
        };
    } 

    /// <summary>
    /// Put it all together.
    /// </summary>
    /// <returns></returns>
    public byte[] GetBytes()
    {
        var ms = new MemoryStream();
        ms.WriteByte((byte)Version.Value);
        ms.WriteByte((byte)Multicodec.Value);
        ms.WriteByte((byte)HashFunction.Value);
        ms.WriteByte((byte)DigestSize.Value);
        ms.Write(DigestBytes, 0, DigestSize.Value);
        return ms.ToArray();
    }

    /// <summary>
    /// Get the base32 representation of the CID.
    /// </summary>
    /// <returns></returns>
    public string GetBase32()
    {
        return "b" + Base32Encoding.BytesToBase32(GetBytes());
    }

    public override string ToString()
    {
        return GetBase32();
    }
}