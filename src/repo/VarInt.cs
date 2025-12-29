
namespace dnproto.repo;

/// <summary>
/// In CAR files, several values are stored as variable-length integers (varints).
/// A varint is a sequence of bytes where the lower 7 bits of each byte are data 
/// and the high bit is a flag indicating whether there are more bytes in the sequence.
/// Use this class to read varints from a stream.
/// 
/// https://protobuf.dev/programming-guides/encoding/#varints
/// 
/// </summary>
public class VarInt
{
    public long Value { get; set; }

    public override string ToString()
    {
        return $"{Value} (hex:0x{Value:X})";
    }

    public static VarInt FromLong(long value)
    {
        VarInt vi = new VarInt();
        vi.Value = value;
        return vi;
    }

    /// <summary>
    /// Read a varint from a stream.
    /// </summary>
    public static VarInt ReadVarInt(Stream fs)
    {
        VarInt ret = new VarInt();
        ret.Value = 0;

        int shift = 0;
        byte b;
        
        do
        {
            b = (byte) fs.ReadByte();
            ret.Value |= (long)(b & 0x7F) << shift;
            shift += 7;
        } 
        // check if the high bit is set.
        // If it is, there are more bytes in the sequence
        while ((b & 0x80) != 0);

        return ret;
    }

    /// <summary>
    /// Write a varint to a stream.
    /// </summary>
    public static void WriteVarInt(Stream fs, VarInt varInt)
    {
        long value = varInt.Value;
        
        while (value >= 0x80)
        {
            // Write the lower 7 bits with the high bit set (continuation flag)
            fs.WriteByte((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        
        // Write the final byte without the high bit set
        fs.WriteByte((byte)value);
    }

    /// <summary>
    /// Write a varint to a stream.
    /// </summary>
    public static async Task WriteVarIntAsync(Stream fs, VarInt varInt)
    {
        long value = varInt.Value;
        
        while (value >= 0x80)
        {
            // Write the lower 7 bits with the high bit set (continuation flag)
            await fs.WriteAsync(new byte[] { (byte)((value & 0x7F) | 0x80) }, 0, 1);
            value >>= 7;
        }
        
        // Write the final byte without the high bit set
        await fs.WriteAsync(new byte[] { (byte)value }, 0, 1);
    }

}

