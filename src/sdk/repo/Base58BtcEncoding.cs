using System;
using System.Linq;
using System.Numerics;
using System.Text;

namespace dnproto.sdk.repo;

/// <summary>
/// Provides Base58BTC encoding functionality using the Bitcoin alphabet.
/// Base58BTC is commonly used for encoding cryptographic keys and addresses.
/// </summary>
public static class Base58BtcEncoding
{
    // Bitcoin Base58 alphabet (no 0, O, I, l to avoid confusion)
    private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private static readonly BigInteger Base = 58;

    /// <summary>
    /// Encodes a byte array into a Base58BTC string.
    /// </summary>
    /// <param name="data">The byte array to encode.</param>
    /// <returns>The Base58BTC encoded string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    public static string Encode(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (data.Length == 0)
            return string.Empty;

        // Count leading zeros
        int leadingZeros = 0;
        while (leadingZeros < data.Length && data[leadingZeros] == 0)
        {
            leadingZeros++;
        }

        // Convert byte array to BigInteger (big-endian)
        var value = new BigInteger(data.Reverse().Concat(new byte[] { 0 }).ToArray());

        // Encode to Base58
        var result = new StringBuilder();
        while (value > 0)
        {
            value = BigInteger.DivRem(value, Base, out BigInteger remainder);
            result.Insert(0, Alphabet[(int)remainder]);
        }

        // Add '1' for each leading zero byte
        for (int i = 0; i < leadingZeros; i++)
        {
            result.Insert(0, Alphabet[0]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Decodes a Base58BTC string back into a byte array.
    /// </summary>
    /// <param name="encoded">The Base58BTC encoded string.</param>
    /// <returns>The decoded byte array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when encoded is null.</exception>
    /// <exception cref="ArgumentException">Thrown when encoded contains invalid characters.</exception>
    public static byte[] Decode(string encoded)
    {
        if (encoded == null)
            throw new ArgumentNullException(nameof(encoded));

        if (encoded.Length == 0)
            return Array.Empty<byte>();

        // Count leading '1's (representing zero bytes)
        int leadingOnes = 0;
        while (leadingOnes < encoded.Length && encoded[leadingOnes] == Alphabet[0])
        {
            leadingOnes++;
        }

        // Decode from Base58
        BigInteger value = 0;
        for (int i = leadingOnes; i < encoded.Length; i++)
        {
            int digit = Alphabet.IndexOf(encoded[i]);
            if (digit < 0)
                throw new ArgumentException($"Invalid character '{encoded[i]}' at position {i}", nameof(encoded));

            value = value * Base + digit;
        }

        // Convert BigInteger to byte array (big-endian)
        var bytes = value.ToByteArray().Reverse().ToArray();

        // Remove any extra zero bytes added by BigInteger
        int startIndex = 0;
        while (startIndex < bytes.Length && bytes[startIndex] == 0)
        {
            startIndex++;
        }

        // Add leading zeros
        var result = new byte[leadingOnes + bytes.Length - startIndex];
        Array.Copy(bytes, startIndex, result, leadingOnes, bytes.Length - startIndex);

        return result;
    }

    /// <summary>
    /// Encodes a byte array into a Base58BTC string with multibase prefix 'z'.
    /// This is the standard format for multibase base58btc encoding.
    /// </summary>
    /// <param name="data">The byte array to encode.</param>
    /// <returns>The multibase Base58BTC encoded string (prefixed with 'z').</returns>
    public static string EncodeMultibase(byte[] data)
    {
        return "z" + Encode(data);
    }

    /// <summary>
    /// Decodes a multibase Base58BTC string (with 'z' prefix) back into a byte array.
    /// </summary>
    /// <param name="encoded">The multibase encoded string (must start with 'z').</param>
    /// <returns>The decoded byte array.</returns>
    /// <exception cref="ArgumentException">Thrown when encoded doesn't start with 'z'.</exception>
    public static byte[] DecodeMultibase(string encoded)
    {
        if (encoded == null)
            throw new ArgumentNullException(nameof(encoded));

        if (encoded.Length == 0 || encoded[0] != 'z')
            throw new ArgumentException("Multibase base58btc string must start with 'z'", nameof(encoded));

        return Decode(encoded.Substring(1));
    }

    /// <summary>
    /// Gets the size in bytes that the encoded string represents.
    /// Useful for debugging key size discrepancies.
    /// </summary>
    /// <param name="encoded">The Base58BTC encoded string.</param>
    /// <returns>The number of bytes the encoded string represents.</returns>
    public static int GetDecodedSize(string encoded)
    {
        if (encoded == null)
            return 0;

        // Remove multibase prefix if present
        if (encoded.StartsWith("z"))
            encoded = encoded.Substring(1);

        if (encoded.Length == 0)
            return 0;

        return Decode(encoded).Length;
    }
}
