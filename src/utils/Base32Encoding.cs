
using System.Text;

namespace dnproto.utils;

public class Base32Encoding
{
    // Take 5 bits at a time and convert to base32 character.
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


    //
    // Clearly not the most efficient way to do this, but it works for now.
    //
    // Take 5 bits at a time, convert to base32 character.
    //
    public static string BytesToBase32Orig(byte[] bytes)
    {
        string base32characters = "abcdefghijklmnopqrstuvwxyz234567";
        string cidBits = string.Join("", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));

        int index = 0;

        StringBuilder sb = new StringBuilder();

        while(index < cidBits.Length-5)
        {
            string next5 = cidBits.Substring(index, 5);
            index += 5;
            int next5Int = Convert.ToInt32(next5, 2);
            char next5Char = base32characters[next5Int];
            sb.Append(next5Char);
        }

        if (index < cidBits.Length)
        {
            string next5 = cidBits.Substring(index).PadRight(5, '0');
            int next5Int = Convert.ToInt32(next5, 2);
            char next5Char = base32characters[next5Int];
            sb.Append(next5Char);
        }

        return sb.ToString();
    }

    
}
