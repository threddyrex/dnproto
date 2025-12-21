using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace dnproto.sdk.repo;

/// <summary>
/// Utilities for working with AT Protocol Record Keys, including TID (Timestamp Identifier) generation.
/// 
/// See: https://atproto.com/specs/record-key
///      https://atproto.com/specs/tid
/// </summary>
public static class RecordKey
{
    // Base32-sortable alphabet for TID encoding
    private const string TidAlphabet = "234567abcdefghijklmnopqrstuvwxyz";
    
    // Valid first characters for TID (high bit must be 0)
    private const string TidFirstCharacters = "234567abcdefghij";
    
    // TID regex pattern
    private static readonly Regex TidPattern = new Regex(@"^[234567abcdefghij][234567abcdefghijklmnopqrstuvwxyz]{12}$", RegexOptions.Compiled);
    
    // Record key validation regex
    private static readonly Regex RecordKeyPattern = new Regex(@"^[A-Za-z0-9._~:.-]{1,512}$", RegexOptions.Compiled);
    
    // Clock identifier for this instance (10 bits = 0-1023)
    private static readonly int ClockIdentifier = RandomNumberGenerator.GetInt32(0, 1024);
    
    // Last timestamp to ensure monotonic increase
    private static long _lastTimestamp = 0;
    private static readonly object _lock = new object();

    /// <summary>
    /// Generate a new TID (Timestamp Identifier) record key.
    /// 
    /// TIDs are 64-bit integers encoded as 13-character base32-sortable strings.
    /// Layout:
    /// - Top 1 bit: always 0
    /// - Next 53 bits: microseconds since UNIX epoch
    /// - Final 10 bits: random clock identifier
    /// </summary>
    /// <param name="timestamp">Optional timestamp in microseconds since UNIX epoch. If null, uses current time.</param>
    /// <returns>A 13-character TID string</returns>
    public static string GenerateTid(long? timestamp = null)
    {
        long tidValue;
        
        lock (_lock)
        {
            long ts;
            if (timestamp.HasValue)
            {
                ts = timestamp.Value;
            }
            else
            {
                // Get current time in microseconds since UNIX epoch
                ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
            }
            
            // Ensure monotonic increase - if we're generating multiple TIDs in the same microsecond
            if (ts <= _lastTimestamp)
            {
                ts = _lastTimestamp + 1;
            }
            _lastTimestamp = ts;
            
            // Build the 64-bit TID value
            // Top bit is 0 (implicit, since we're using long which is signed)
            // Next 53 bits are timestamp
            // Last 10 bits are clock identifier
            
            // Ensure timestamp fits in 53 bits (mask to ensure no overflow)
            ts = ts & 0x1FFFFFFFFFFFFF; // 53 bits set
            
            // Shift timestamp left by 10 bits to make room for clock identifier
            tidValue = ts << 10;
            
            // Add clock identifier (10 bits)
            tidValue = tidValue | (long)ClockIdentifier;
        }
        
        // Convert to base32-sortable encoding
        return EncodeBase32Sortable(tidValue);
    }
    
    /// <summary>
    /// Decode a TID string back to its 64-bit integer value.
    /// </summary>
    /// <param name="tid">The TID string to decode</param>
    /// <returns>The 64-bit integer value</returns>
    /// <exception cref="ArgumentException">Thrown if the TID format is invalid</exception>
    public static long DecodeTid(string tid)
    {
        if (!IsValidTid(tid))
        {
            throw new ArgumentException($"Invalid TID format: {tid}");
        }
        
        return DecodeBase32Sortable(tid);
    }
    
    /// <summary>
    /// Extract the timestamp (in microseconds since UNIX epoch) from a TID.
    /// </summary>
    /// <param name="tid">The TID string</param>
    /// <returns>Microseconds since UNIX epoch</returns>
    public static long GetTidTimestamp(string tid)
    {
        long tidValue = DecodeTid(tid);
        // Right shift by 10 to remove clock identifier bits
        return tidValue >> 10;
    }
    
    /// <summary>
    /// Extract the clock identifier from a TID.
    /// </summary>
    /// <param name="tid">The TID string</param>
    /// <returns>The 10-bit clock identifier (0-1023)</returns>
    public static int GetTidClockIdentifier(string tid)
    {
        long tidValue = DecodeTid(tid);
        // Mask to get only the last 10 bits
        return (int)(tidValue & 0x3FF);
    }
    
    /// <summary>
    /// Validate if a string is a valid TID.
    /// </summary>
    /// <param name="tid">The string to validate</param>
    /// <returns>True if valid TID format</returns>
    public static bool IsValidTid(string? tid)
    {
        if (string.IsNullOrEmpty(tid))
        {
            return false;
        }
        
        return TidPattern.IsMatch(tid);
    }
    
    /// <summary>
    /// Validate if a string is a valid record key (generic).
    /// </summary>
    /// <param name="key">The string to validate</param>
    /// <returns>True if valid record key format</returns>
    public static bool IsValidRecordKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }
        
        // Check basic pattern
        if (!RecordKeyPattern.IsMatch(key))
        {
            return false;
        }
        
        // Reject "." and ".."
        if (key == "." || key == "..")
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Encode a 64-bit integer as a 13-character base32-sortable string.
    /// </summary>
    private static string EncodeBase32Sortable(long value)
    {
        // TID is always 13 characters (65 bits / 5 bits per char = 13)
        char[] result = new char[13];
        
        // Process from right to left (least significant to most significant)
        for (int i = 12; i >= 0; i--)
        {
            int index = (int)(value & 0x1F); // Get lowest 5 bits
            result[i] = TidAlphabet[index];
            value >>= 5; // Shift right by 5 bits
        }
        
        return new string(result);
    }
    
    /// <summary>
    /// Decode a base32-sortable string back to a 64-bit integer.
    /// </summary>
    private static long DecodeBase32Sortable(string encoded)
    {
        if (encoded.Length != 13)
        {
            throw new ArgumentException($"TID must be exactly 13 characters, got {encoded.Length}");
        }
        
        long result = 0;
        
        foreach (char c in encoded)
        {
            int index = TidAlphabet.IndexOf(c);
            if (index == -1)
            {
                throw new ArgumentException($"Invalid base32-sortable character: {c}");
            }
            
            result = (result << 5) | (long)index;
        }
        
        return result;
    }
}
