using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace dnproto.auth;

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
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = signingCredentials,
            AdditionalHeaderClaims = new Dictionary<string, object> { { "typ", "at+jwt" } }
        };
        
        var tokenHandler = new JsonWebTokenHandler();
        return tokenHandler.CreateToken(tokenDescriptor);
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
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = signingCredentials,
            AdditionalHeaderClaims = new Dictionary<string, object> { { "typ", "refresh+jwt" } }
        };
        
        var tokenHandler = new JsonWebTokenHandler();
        return tokenHandler.CreateToken(tokenDescriptor);
    }

    /// <summary>
    /// Verify a refresh JWT token.
    /// Validates the signature, expiration, and scope of the refresh token.
    /// </summary>
    /// <param name="refreshJwt">The refresh JWT token from the client</param>
    /// <param name="jwtSecret">The JWT secret from PdsConfig</param>
    /// <returns>ClaimsPrincipal if valid, null if invalid</returns>
    public static ClaimsPrincipal? VerifyRefreshJwt(string? refreshJwt, string jwtSecret)
    {
        if (string.IsNullOrEmpty(refreshJwt) || string.IsNullOrEmpty(jwtSecret))
        {
            return null;
        }

        var tokenHandler = new JsonWebTokenHandler();
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

            var result = tokenHandler.ValidateTokenAsync(refreshJwt, validationParameters).GetAwaiter().GetResult();
            
            if (!result.IsValid)
            {
                return null;
            }

            // Verify the algorithm is HMAC SHA256
            if (result.SecurityToken is JsonWebToken jwt && 
                !jwt.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            // Verify the scope is for refresh tokens
            var principal = new ClaimsPrincipal(result.ClaimsIdentity);
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
    /// <param name="userDid">The DID of the user to validate against</param>
    /// <param name="validateExpiry">Whether to validate the token's expiration</param>
    /// <returns>ClaimsPrincipal if valid, null if invalid</returns>
    public static bool AccessJwtIsValid(string? accessJwt, string jwtSecret, string userDid, bool validateExpiry = true)
    {
        if (string.IsNullOrEmpty(accessJwt) || string.IsNullOrEmpty(jwtSecret) || string.IsNullOrEmpty(userDid))
        {
            return false;
        }

        var tokenHandler = new JsonWebTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtSecret);

        try
        {
            //
            // JWT validation
            //
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false, // We don't set an issuer
                ValidateAudience = false, // We validate audience manually if needed
                ValidateLifetime = validateExpiry, // Check expiry only if validateExpiry is true
                ClockSkew = TimeSpan.Zero // No clock skew tolerance
            };

            TokenValidationResult result = tokenHandler.ValidateTokenAsync(accessJwt, validationParameters).GetAwaiter().GetResult();
            
            if (!result.IsValid)
            {
                return false;
            }

            //
            // Verify the algorithm is HMAC SHA256
            //
            if (result.SecurityToken is JsonWebToken jwt && 
                !jwt.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            //
            // Verify the scope is for access tokens
            //
            var claimsPrincipal = new ClaimsPrincipal(result.ClaimsIdentity);
            var scopeClaim = claimsPrincipal.FindFirst("scope")?.Value;
            if (scopeClaim != "com.atproto.access")
            {
                return false;
            }

            //
            // Check DID
            //
            string? claimsDid = GetDidFromClaimsPrincipal(claimsPrincipal);
            bool didMatches = claimsDid == userDid;

            if(!didMatches)
            {
                return false;
            }


            //
            // Return true
            //
            return true;
        }
        catch (SecurityTokenException)
        {
            // Token validation failed (expired, invalid signature, etc.)
            return false;
        }
        catch (Exception)
        {
            // Any other exception during validation
            return false;
        }
    }

    public static DateTime GetExpirationDateForAccessJwt(string accessJwt)
    {
        if (string.IsNullOrEmpty(accessJwt))
        {
            throw new ArgumentException("Access JWT cannot be null or empty.", nameof(accessJwt));
        }

        var tokenHandler = new JsonWebTokenHandler();
        var jwt = tokenHandler.ReadJsonWebToken(accessJwt);
        if (jwt == null)
        {
            throw new ArgumentException("Invalid JWT token.", nameof(accessJwt));
        }

        return jwt.ValidTo;
    }


    public static string? GetDidFromClaimsPrincipal(ClaimsPrincipal? claimsPrincipal)
    {
        if(claimsPrincipal == null)
        {
            return null;
        }

        // Check both ClaimTypes.NameIdentifier and "sub" for compatibility
        var subClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)
                    ?? claimsPrincipal.FindFirst("sub");
        
        if(subClaim == null)
        {
            return null;
        }

        if(string.IsNullOrEmpty(subClaim.Value))
        {
            return null;
        }

        if(!subClaim.Value.StartsWith("did:"))
        {
            return null;
        }

        return subClaim.Value;
    }   
}