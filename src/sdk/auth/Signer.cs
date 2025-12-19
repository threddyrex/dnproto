using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using dnproto.sdk.repo;
using dnproto.sdk.log;

namespace dnproto.sdk.crypto;

/// <summary>
/// Signs and validates JWT tokens using RSA or ECDSA cryptographic keys.
/// </summary>
public static class Signer
{
    /// <summary>
    /// Signs a JWT token with the specified claims and optional expiration time.
    /// </summary>
    /// <param name="publicKey">The public key in hex or PEM format</param>
    /// <param name="privateKey">The private key in hex or PEM format</param>
    /// <param name="issuer">The JWT issuer claim</param>
    /// <param name="audience">The JWT audience claim</param>
    /// <param name="claims">Additional claims to include in the token (optional)</param>
    /// <param name="expiresInSeconds">Token expiration time in seconds (default: 180)</param>
    /// <returns>A signed JWT token string</returns>
    public static string SignToken(string publicKey, string privateKey, string issuer, string audience, Dictionary<string, string>? claims = null, int expiresInSeconds = 180)
    {
        // Create a list of claims
        var claimsList = new List<Claim>
        {
            new("lxm", "com.atproto.server.createAccount"),
            new(JwtRegisteredClaimNames.Iss, issuer),
            new(JwtRegisteredClaimNames.Aud, audience),
        };

        // Add any additional claims
        if (claims != null)
        {
            foreach (var claim in claims)
            {
                claimsList.Add(new Claim(claim.Key, claim.Value));
            }
        }

        // Try to parse as RSA key first, then fall back to ECDSA
        SigningCredentials signingCredentials;
        
        bool isHexFormat = !privateKey.Contains("-----BEGIN");
        
        try
        {
            if (isHexFormat)
            {
                // Hex format - raw key bytes, assume ECDSA P-256
                var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var privateKeyBytes = Convert.FromHexString(privateKey);
                var publicKeyBytes = Convert.FromHexString(publicKey);
                
                // Handle compressed or uncompressed public key
                ECPoint publicPoint;
                if (publicKeyBytes.Length == 33 && (publicKeyBytes[0] == 0x02 || publicKeyBytes[0] == 0x03))
                {
                    // Compressed format (0x02/0x03 + 32-byte X)
                    // Decompress by computing Y from the curve equation
                    var x = publicKeyBytes.Skip(1).Take(32).ToArray();
                    var y = DecompressPublicKey(x, publicKeyBytes[0] == 0x03);
                    publicPoint = new ECPoint { X = x, Y = y };
                }
                else if (publicKeyBytes.Length == 65 && publicKeyBytes[0] == 0x04)
                {
                    // Uncompressed format (0x04 + 32-byte X + 32-byte Y)
                    publicPoint = new ECPoint
                    {
                        X = publicKeyBytes.Skip(1).Take(32).ToArray(),
                        Y = publicKeyBytes.Skip(33).Take(32).ToArray()
                    };
                }
                else
                {
                    throw new InvalidOperationException("Invalid public key format. Expected 33-byte compressed or 65-byte uncompressed key.");
                }
                
                var parameters = new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    D = privateKeyBytes,
                    Q = publicPoint
                };
                
                ecdsa.ImportParameters(parameters);
                var ecdsaKey = new ECDsaSecurityKey(ecdsa);
                signingCredentials = new SigningCredentials(ecdsaKey, SecurityAlgorithms.EcdsaSha256);
            }
            else
            {
                // PEM format - try RSA first
                try
                {
                    var rsa = RSA.Create();
                    rsa.ImportFromPem(privateKey);
                    var rsaKey = new RsaSecurityKey(rsa.ExportParameters(true));
                    signingCredentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
                }
                catch
                {
                    // Try ECDSA
                    var ecdsa = ECDsa.Create();
                    ecdsa.ImportFromPem(privateKey);
                    var ecdsaKey = new ECDsaSecurityKey(ecdsa);
                    signingCredentials = new SigningCredentials(ecdsaKey, SecurityAlgorithms.EcdsaSha256);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to import private key. Ensure it's in valid PEM or hex format.", ex);
        }

        // Create the token descriptor
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claimsList),
            Expires = DateTime.UtcNow.AddSeconds(expiresInSeconds),
            SigningCredentials = signingCredentials,
            IssuedAt = null,  // Prevent automatic iat claim
            NotBefore = null  // Prevent automatic nbf claim
        };

        // Create and sign the token
        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.SetDefaultTimesOnTokenCreation = false;  // Disable automatic timestamp claims
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Validates an incoming JWT token from an Authorization header.
    /// Caller needs to look up user's public key in did doc.
    /// </summary>
    /// <param name="token">The JWT token string (without "Bearer " prefix)</param>
    /// <param name="issuerPublicKey">The public key of the expected issuer in multibase, hex, or PEM format</param>
    /// <param name="expectedIssuer">The expected issuer (iss claim)</param>
    /// <param name="expectedAudience">The expected audience (aud claim)</param>
    /// <param name="logger">Optional logger for error reporting</param>
    /// <returns>A ClaimsPrincipal if valid, null if validation fails</returns>
    public static ClaimsPrincipal? ValidateToken(string token, string issuerPublicKey, string expectedIssuer, string expectedAudience, BaseLogger? logger = null)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            
            // Create security key from public key
            SecurityKey securityKey;
        
        // Determine format: multibase (starts with 'z'), PEM (contains -----BEGIN), or hex
        if (issuerPublicKey.StartsWith('z'))
        {
            // Multibase format (from DID document)
            var publicKeyBytes = Base58BtcEncoding.DecodeMultibase(issuerPublicKey);
            
            // Remove multicodec prefix (first 2 bytes for P-256: 0x80 0x24)
            byte[] keyBytes;
            if (publicKeyBytes.Length > 2 && publicKeyBytes[0] == 0x80 && publicKeyBytes[1] == 0x24)
            {
                keyBytes = publicKeyBytes.Skip(2).ToArray();
            }
            else
            {
                keyBytes = publicKeyBytes;
            }
            
            var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            
            // Handle compressed or uncompressed public key
            ECPoint publicPoint;
            if (keyBytes.Length == 33 && (keyBytes[0] == 0x02 || keyBytes[0] == 0x03))
            {
                var x = keyBytes.Skip(1).Take(32).ToArray();
                var y = DecompressPublicKey(x, keyBytes[0] == 0x03);
                publicPoint = new ECPoint { X = x, Y = y };
            }
            else if (keyBytes.Length == 65 && keyBytes[0] == 0x04)
            {
                publicPoint = new ECPoint
                {
                    X = keyBytes.Skip(1).Take(32).ToArray(),
                    Y = keyBytes.Skip(33).Take(32).ToArray()
                };
            }
            else
            {
                throw new InvalidOperationException($"Invalid public key format. Expected 33 or 65 bytes, got {keyBytes.Length}.");
            }
            
            var parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = publicPoint
            };
            
            ecdsa.ImportParameters(parameters);
            securityKey = new ECDsaSecurityKey(ecdsa);
        }
        else if (issuerPublicKey.Contains("-----BEGIN"))
        {
            // PEM format - try RSA first, then ECDSA
            try
            {
                var rsa = RSA.Create();
                rsa.ImportFromPem(issuerPublicKey);
                securityKey = new RsaSecurityKey(rsa);
            }
            catch
            {
                var ecdsa = ECDsa.Create();
                ecdsa.ImportFromPem(issuerPublicKey);
                securityKey = new ECDsaSecurityKey(ecdsa);
            }
        }
        else
        {
            // Hex format - assume ECDSA P-256
            var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var publicKeyBytes = Convert.FromHexString(issuerPublicKey);
            
            // Handle compressed or uncompressed public key
            ECPoint publicPoint;
            if (publicKeyBytes.Length == 33 && (publicKeyBytes[0] == 0x02 || publicKeyBytes[0] == 0x03))
            {
                var x = publicKeyBytes.Skip(1).Take(32).ToArray();
                var y = DecompressPublicKey(x, publicKeyBytes[0] == 0x03);
                publicPoint = new ECPoint { X = x, Y = y };
            }
            else if (publicKeyBytes.Length == 65 && publicKeyBytes[0] == 0x04)
            {
                publicPoint = new ECPoint
                {
                    X = publicKeyBytes.Skip(1).Take(32).ToArray(),
                    Y = publicKeyBytes.Skip(33).Take(32).ToArray()
                };
            }
            else
            {
                throw new InvalidOperationException("Invalid public key format.");
            }
            
            var parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = publicPoint
            };
            
            ecdsa.ImportParameters(parameters);
            securityKey = new ECDsaSecurityKey(ecdsa);
        }
        
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = expectedIssuer,
            ValidateAudience = true,
            ValidAudience = expectedAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 minute clock skew
        };
        
            return tokenHandler.ValidateToken(token, validationParameters, out _);
        }
        catch (Exception ex)
        {
            logger?.LogError($"JWT validation failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts claims from a validated JWT token without validation.
    /// Use ValidateToken first to ensure the token is valid.
    /// </summary>
    /// <param name="token">The JWT token string</param>
    /// <returns>Dictionary of claim types to values</returns>
    public static Dictionary<string, string> GetClaims(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);
        
        return jwtToken.Claims.ToDictionary(c => c.Type, c => c.Value);
    }

    /// <summary>
    /// Extracts a specific claim value from a JWT token without validation.
    /// </summary>
    /// <param name="token">The JWT token string</param>
    /// <param name="claimType">The claim type to extract (e.g., "lxm", "iss", "aud")</param>
    /// <returns>The claim value, or null if not found</returns>
    public static string? GetClaim(string token, string claimType)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);
        
        return jwtToken.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
    }

    /// <summary>
    /// Decompresses a compressed P-256 public key by computing Y from X using the curve equation.
    /// </summary>
    private static byte[] DecompressPublicKey(byte[] x, bool isOdd)
    {
        // P-256 curve parameters
        var p = System.Numerics.BigInteger.Parse("115792089210356248762697446949407573530086143415290314195533631308867097853951");
        var a = System.Numerics.BigInteger.Parse("115792089210356248762697446949407573530086143415290314195533631308867097853948");
        var b = System.Numerics.BigInteger.Parse("41058363725152142129326129780047268409114441015993725554835256314039467401291");
        
        // Convert X to BigInteger
        var xInt = new System.Numerics.BigInteger(x, isUnsigned: true, isBigEndian: true);
        
        // Compute Y^2 = X^3 + aX + b (mod p)
        var ySquared = (System.Numerics.BigInteger.ModPow(xInt, 3, p) + a * xInt + b) % p;
        
        // Compute Y = sqrt(Y^2) mod p using Tonelli-Shanks
        var y = ModSqrt(ySquared, p);
        
        // Choose the correct root based on the compression flag
        var yIsOdd = !y.IsEven;
        if (yIsOdd != isOdd)
        {
            y = p - y;
        }
        
        // Convert back to 32-byte array
        var yBytes = y.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (yBytes.Length < 32)
        {
            var padded = new byte[32];
            Array.Copy(yBytes, 0, padded, 32 - yBytes.Length, yBytes.Length);
            return padded;
        }
        return yBytes.Take(32).ToArray();
    }

    /// <summary>
    /// Computes modular square root using Tonelli-Shanks algorithm for P-256 prime.
    /// </summary>
    private static System.Numerics.BigInteger ModSqrt(System.Numerics.BigInteger a, System.Numerics.BigInteger p)
    {
        // For P-256, p ≡ 3 (mod 4), so we can use the simple formula: sqrt(a) = a^((p+1)/4) mod p
        return System.Numerics.BigInteger.ModPow(a, (p + 1) / 4, p);
    }
}

