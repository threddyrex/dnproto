
using System.Text;

namespace dnproto.repo;


public class Base32Encoding
{
    /// <summary>
    /// Take 5 bits at a time and convert to base32 character.
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static string BytesToBase32(byte[] bytes)
    {
        int currentByteIndex = 0;
        int bitsRemaining = 8;
        string charMap = "abcdefghijklmnopqrstuvwxyz234567";
        StringBuilder sb = new StringBuilder();

        while(bitsRemaining > 0)
        {
            if(bitsRemaining >= 5)
            {
                int next5Int = (bytes[currentByteIndex] >> (bitsRemaining - 5)) & 0x1F;
                sb.Append(charMap[next5Int]);
                bitsRemaining -= 5;

                if(bitsRemaining == 0 && currentByteIndex + 1 < bytes.Length)
                {
                    currentByteIndex++;
                    bitsRemaining = 8;
                }
            }
            else
            {
                if(currentByteIndex + 1 < bytes.Length)
                {
                    int next5int = bytes[currentByteIndex];
                    // shift left to get the bits we need
                    next5int = next5int << (5 - bitsRemaining);
                    // mask out the rest
                    next5int = next5int & 0x1F;
                    // get the next byte
                    int next5int2 = bytes[currentByteIndex + 1];
                    // shift right to get the bits we need
                    next5int2 = next5int2 >> (8 - (5 - bitsRemaining));
                    // mask out the rest
                    next5int2 = next5int2 & 0x1F;
                    // combine those two
                    next5int = next5int | next5int2;
                    sb.Append(charMap[next5int]);
                    // move to the next byte
                    currentByteIndex++;
                    // figure out bitsremaining
                    bitsRemaining = 8 - (5 - bitsRemaining);
                }
                else
                {
                    // this is the last one
                    // get final byte
                    int next5int = bytes[currentByteIndex];
                    // shift left to get the bits we need
                    next5int = next5int << (5 - bitsRemaining);
                    // mask out the rest
                    next5int = next5int & 0x1F;
                    sb.Append(charMap[next5int]);
                    bitsRemaining = 0; // end
                }
            }
        }


        return sb.ToString();
    }

    /// <summary>
    /// Decode a base32 string back to bytes.
    /// This is the inverse of BytesToBase32.
    /// </summary>
    /// <param name="base32String">The base32 encoded string</param>
    /// <returns>The decoded bytes</returns>
    public static byte[] Base32ToBytes(string base32String)
    {
        if (string.IsNullOrEmpty(base32String))
        {
            return Array.Empty<byte>();
        }

        string charMap = "abcdefghijklmnopqrstuvwxyz234567";
        
        // Calculate output size: 5 bits per character, pack into 8-bit bytes
        // Use floor division since trailing bits are padding
        int bitCount = base32String.Length * 5;
        int byteCount = bitCount / 8; // Floor division - ignore padding bits
        byte[] result = new byte[byteCount];

        int currentByte = 0;
        int bitsInCurrentByte = 0;

        foreach (char c in base32String)
        {
            // Stop if we've filled all the bytes we need
            if (currentByte >= byteCount)
            {
                break;
            }

            // Get the 5-bit value for this character
            int value = charMap.IndexOf(char.ToLower(c));
            if (value == -1)
            {
                throw new ArgumentException($"Invalid base32 character: {c}");
            }

            // We have 5 bits to add to our output
            int bitsToAdd = 5;

            while (bitsToAdd > 0 && currentByte < byteCount)
            {
                int bitsAvailableInCurrentByte = 8 - bitsInCurrentByte;

                if (bitsToAdd >= bitsAvailableInCurrentByte)
                {
                    // We can fill the current byte (or more)
                    int bitsToTake = bitsAvailableInCurrentByte;
                    int shift = bitsToAdd - bitsToTake;
                    int mask = (1 << bitsToTake) - 1;
                    int bitsValue = (value >> shift) & mask;

                    result[currentByte] |= (byte)(bitsValue << (8 - bitsInCurrentByte - bitsToTake));

                    bitsInCurrentByte += bitsToTake;
                    bitsToAdd -= bitsToTake;

                    if (bitsInCurrentByte == 8)
                    {
                        currentByte++;
                        bitsInCurrentByte = 0;
                    }
                }
                else
                {
                    // We can't fill the current byte, just add what we have
                    int mask = (1 << bitsToAdd) - 1;
                    int bitsValue = value & mask;

                    result[currentByte] |= (byte)(bitsValue << (8 - bitsInCurrentByte - bitsToAdd));

                    bitsInCurrentByte += bitsToAdd;
                    bitsToAdd = 0;
                }
            }
        }

        return result;
    }
}
