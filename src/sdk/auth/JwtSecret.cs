using System.Security.Cryptography;

namespace dnproto.sdk.auth;

public class JwtSecret
{
    /// <summary>
    /// Generates a cryptographically secure random JWT secret.
    /// Equivalent to "openssl rand --hex 16" - generates 16 random bytes as a 32-character hex string.
    /// </summary>
    /// <returns>A 32-character hexadecimal string suitable for use as a JWT secret</returns>
    public static string GenerateJwtSecret()
    {
        // Generate 16 random bytes (128 bits)
        byte[] randomBytes = RandomNumberGenerator.GetBytes(16);
        
        // Convert to lowercase hexadecimal string (32 characters)
        return Convert.ToHexString(randomBytes).ToLowerInvariant();
    }
}