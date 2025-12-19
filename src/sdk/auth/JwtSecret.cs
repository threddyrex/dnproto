using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

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

    /// <summary>
    /// Generate an access JWT token.
    /// Access tokens are short-lived (2 hours) and used for API authentication.
    /// </summary>
    public static string? GenerateAccessJwt(string? userDid, string pdsDid, string jwtSecret)
    {
        if(string.IsNullOrEmpty(userDid))
        {
            return null;
        }

        var key = Encoding.UTF8.GetBytes(jwtSecret);
        
        var now = DateTimeOffset.UtcNow;
        var expiry = now.AddHours(2); // Access tokens expire in 2 hours
        
        var claims = new List<Claim>
        {
            new Claim("scope", "com.atproto.access"),
            new Claim(JwtRegisteredClaimNames.Aud, pdsDid),
            new Claim(JwtRegisteredClaimNames.Sub, userDid),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Exp, expiry.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256);
        
        // Create JWT token with custom header
        var header = new JwtHeader(signingCredentials);
        header["typ"] = "at+jwt"; // Override the default "JWT" type
        
        var payload = new JwtPayload(claims);
        
        var jwtToken = new JwtSecurityToken(header, payload);
        var tokenHandler = new JwtSecurityTokenHandler();
        
        return tokenHandler.WriteToken(jwtToken);
    }

    /// <summary>
    /// Generate a refresh JWT token.
    /// Refresh tokens are long-lived (90 days) and used to obtain new access tokens.
    /// </summary>
    public static string? GenerateRefreshJwt(string? userDid, string pdsDid, string jwtSecret)
    {
        if(string.IsNullOrEmpty(userDid))
        {
            return null;
        }

        var key = Encoding.UTF8.GetBytes(jwtSecret);
        
        var now = DateTimeOffset.UtcNow;
        var expiry = now.AddDays(90); // Refresh tokens expire in 90 days
        
        // Generate a unique JTI (JWT ID) for the refresh token
        var jti = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        
        var claims = new List<Claim>
        {
            new Claim("scope", "com.atproto.refresh"),
            new Claim(JwtRegisteredClaimNames.Aud, pdsDid),
            new Claim(JwtRegisteredClaimNames.Sub, userDid),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Exp, expiry.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256);
        
        // Create JWT token with custom header
        var header = new JwtHeader(signingCredentials);
        header["typ"] = "refresh+jwt"; // Override the default "JWT" type
        
        var payload = new JwtPayload(claims);
        
        var jwtToken = new JwtSecurityToken(header, payload);
        var tokenHandler = new JwtSecurityTokenHandler();
        
        return tokenHandler.WriteToken(jwtToken);
    }
}