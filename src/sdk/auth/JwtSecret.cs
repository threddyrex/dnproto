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

    /// <summary>
    /// Verify a refresh JWT token.
    /// Validates the signature, expiration, and scope of the refresh token.
    /// </summary>
    /// <param name="refreshJwt">The refresh JWT token from the client</param>
    /// <param name="jwtSecret">The JWT secret from PdsConfig</param>
    /// <returns>ClaimsPrincipal if valid, null if invalid</returns>
    public static ClaimsPrincipal? VerifyRefreshJwt(string refreshJwt, string jwtSecret)
    {
        if (string.IsNullOrEmpty(refreshJwt) || string.IsNullOrEmpty(jwtSecret))
        {
            return null;
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtSecret);

        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false, // We don't set an issuer
                ValidateAudience = false, // We validate audience manually if needed
                ValidateLifetime = true, // Ensure token hasn't expired
                ClockSkew = TimeSpan.Zero // No clock skew tolerance
            };

            var principal = tokenHandler.ValidateToken(refreshJwt, validationParameters, out SecurityToken validatedToken);

            // Verify it's a JWT token
            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                return null;
            }

            // Verify the algorithm is HMAC SHA256
            if (!jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            // Verify the scope is for refresh tokens
            var scopeClaim = principal.FindFirst("scope")?.Value;
            if (scopeClaim != "com.atproto.refresh")
            {
                return null;
            }

            return principal;
        }
        catch (SecurityTokenException)
        {
            // Token validation failed (expired, invalid signature, etc.)
            return null;
        }
        catch (Exception)
        {
            // Any other exception during validation
            return null;
        }
    }

    /// <summary>
    /// Verify an access JWT token.
    /// Validates the signature, expiration, and scope of the access token.
    /// </summary>
    /// <param name="accessJwt">The access JWT token from the client</param>
    /// <param name="jwtSecret">The JWT secret from PdsConfig</param>
    /// <returns>ClaimsPrincipal if valid, null if invalid</returns>
    public static ClaimsPrincipal? VerifyAccessJwt(string accessJwt, string jwtSecret)
    {
        if (string.IsNullOrEmpty(accessJwt) || string.IsNullOrEmpty(jwtSecret))
        {
            return null;
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtSecret);

        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false, // We don't set an issuer
                ValidateAudience = false, // We validate audience manually if needed
                ValidateLifetime = true, // Ensure token hasn't expired
                ClockSkew = TimeSpan.Zero // No clock skew tolerance
            };

            var principal = tokenHandler.ValidateToken(accessJwt, validationParameters, out SecurityToken validatedToken);

            // Verify it's a JWT token
            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                return null;
            }

            // Verify the algorithm is HMAC SHA256
            if (!jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            // Verify the scope is for access tokens
            var scopeClaim = principal.FindFirst("scope")?.Value;
            if (scopeClaim != "com.atproto.access")
            {
                return null;
            }

            return principal;
        }
        catch (SecurityTokenException)
        {
            // Token validation failed (expired, invalid signature, etc.)
            return null;
        }
        catch (Exception)
        {
            // Any other exception during validation
            return null;
        }
    }
}