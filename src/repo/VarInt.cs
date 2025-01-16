
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
    public int Length { get; set; }
    public int Value { get; set; }

    public override string ToString()
    {
        return $"{Value} (length:{Length} hex:0x{Value:X})";
    }

    /// <summary>
    /// Read a varint from a stream.
    /// </summary>
    public static VarInt ReadVarInt(Stream fs)
    {
        VarInt ret = new VarInt();
        ret.Value = 0;
        ret.Length = 0;

        int shift = 0;
        byte b;
        
        do
        {
            ret.Length++;
            b = (byte) fs.ReadByte();
            ret.Value |= (b & 0x7F) << shift;
            shift += 7;
        } 
        // check if the high bit is set.
        // If it is, there are more bytes in the sequence
        while ((b & 0x80) != 0);

        return ret;
    }

}

