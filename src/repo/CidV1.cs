
using System.Security.Cryptography;

namespace dnproto.repo;

/// <summary>
/// Represents a cid in atproto. Only cid version 1 is supported.
/// https://github.com/multiformats/cid
/// https://github.com/multiformats/multicodec/blob/master/table.csv
/// </summary>
public class CidV1
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


    public static CidV1 ReadCid(Stream s)
    {
        // https://github.com/multiformats/cid
        VarInt version = VarInt.ReadVarInt(s);
        VarInt multicodec = VarInt.ReadVarInt(s);
        VarInt hashFunction = VarInt.ReadVarInt(s); // likely sha2-256, 0x12, decimal 18
        VarInt digestSize = VarInt.ReadVarInt(s);

        if (version.Value != 1)
        {
            throw new Exception($"The first byte should be 1. This class supports only CidV1. First byte found: {version.Value}");
        }
        
        // https://github.com/multiformats/multicodec/blob/master/table.csv
        // dag-cbor = 0x71
        // raw = 0x55
        // should not happen for AT
        if(multicodec.Value != 0x71 && multicodec.Value != 0x55)
        {
            throw new Exception($"cidMulticodec.Value != 0x71 or 0x55: {multicodec.Value}");
        }

        byte[] digestBytes = new byte[digestSize.Value];
        int cidDigestBytesRead = s.Read(digestBytes, 0, (int)digestSize.Value);

        var ms = new MemoryStream();
        ms.WriteByte((byte)version.Value);
        ms.WriteByte((byte)multicodec.Value);
        ms.WriteByte((byte)hashFunction.Value);
        ms.WriteByte((byte)digestSize.Value);
        ms.Write(digestBytes, 0, (int)digestSize.Value);
        byte[] allBytes = ms.ToArray();

        string base32 = "b" + Base32Encoding.BytesToBase32(allBytes);

        return new CidV1
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

    public static void WriteCid(Stream s, CidV1 cid)
    {
        VarInt.WriteVarInt(s, cid.Version);
        VarInt.WriteVarInt(s, cid.Multicodec);
        VarInt.WriteVarInt(s, cid.HashFunction);
        VarInt.WriteVarInt(s, cid.DigestSize);
        s.Write(cid.DigestBytes, 0, cid.DigestBytes.Length);
    }

    public static async Task WriteCidAsync(Stream s, CidV1 cid)
    {
        await VarInt.WriteVarIntAsync(s, cid.Version);
        await VarInt.WriteVarIntAsync(s, cid.Multicodec);
        await VarInt.WriteVarIntAsync(s, cid.HashFunction);
        await VarInt.WriteVarIntAsync(s, cid.DigestSize);
        await s.WriteAsync(cid.DigestBytes, 0, cid.DigestBytes.Length);
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

    public static CidV1 GenerateForBlobBytes(byte[] blobBytes)
    {
        //
        // Compute SHA-256 hash
        //
        var hash = SHA256.HashData(blobBytes);

        //
        // Create CID for blob (using "raw" codec 0x55)
        //
        var cid = new CidV1
        {
            Version = new VarInt { Value = 1 },
            Multicodec = new VarInt { Value = 0x55 }, // raw codec for blobs
            HashFunction = new VarInt { Value = 0x12 }, // SHA-256
            DigestSize = new VarInt { Value = 32 },
            DigestBytes = hash,
            AllBytes = Array.Empty<byte>(),
            Base32 = ""
        };

        //
        // Encode CID as bytes and base32
        //
        using var ms = new MemoryStream();
        CidV1.WriteCid(ms, cid);
        cid.AllBytes = ms.ToArray();
        cid.Base32 = "b" + Base32Encoding.BytesToBase32(cid.AllBytes);

        return cid;
    }

    public static CidV1 FromBase32(string base32)
    {
        if (!base32.StartsWith("b"))
        {
            throw new Exception("CID base32 string must start with 'b'");
        }

        byte[] originalBytes = Base32Encoding.Base32ToBytes(base32.Substring(1));
        using var ms = new MemoryStream(originalBytes);
        var cid = ReadCid(ms);
        
        // Use the original bytes instead of reconstructed ones to preserve exact encoding
        cid.AllBytes = originalBytes;
        cid.Base32 = base32;
        
        return cid;
    }

    public static CidV1 ComputeCidForDagCbor(DagCborObject dagCborObject)
    {
        var bytes = dagCborObject.ToBytes();
        var hash = SHA256.HashData(bytes);

        // Create CIDv1 with dag-cbor multicodec (0x71) and sha256 (0x12)
        var cid = new CidV1
        {
            Version = new VarInt { Value = 1 },
            Multicodec = new VarInt { Value = 0x71 },
            HashFunction = new VarInt { Value = 0x12 },
            DigestSize = new VarInt { Value = 32 },
            DigestBytes = hash,
            AllBytes = Array.Empty<byte>(), // Will be set below
            Base32 = ""
        };

        using var ms = new MemoryStream();
        CidV1.WriteCid(ms, cid);
        cid.AllBytes = ms.ToArray();
        cid.Base32 = "b" + Base32Encoding.BytesToBase32(cid.AllBytes);

        return cid;
    }

    public override bool Equals(object? obj)
    {
        if (obj is CidV1 otherCid)
        {
            return string.Equals(this.Base32, otherCid.Base32, StringComparison.Ordinal);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return Base32.GetHashCode();
    }

}