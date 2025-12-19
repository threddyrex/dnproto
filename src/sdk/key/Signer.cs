using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace dnproto.sdk.key;

/// <summary>
/// Signs JWT tokens using RSA or ECDSA cryptographic keys.
/// </summary>
public class Signer
{
    private readonly string _publicKey;
    private readonly string _privateKey;
    private readonly string _issuer;
    private readonly string _audience;

    /// <summary>
    /// Initializes a new instance of the Signer class.
    /// </summary>
    /// <param name="publicKey">The public key in PEM format</param>
    /// <param name="privateKey">The private key in PEM format</param>
    /// <param name="issuer">The JWT issuer claim</param>
    /// <param name="audience">The JWT audience claim</param>
    public Signer(string publicKey, string privateKey, string issuer, string audience)
    {
        _publicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
        _privateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
        _issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
        _audience = audience ?? throw new ArgumentNullException(nameof(audience));
    }

    /// <summary>
    /// Signs a JWT token with the specified claims and optional expiration time.
    /// </summary>
    /// <param name="claims">Additional claims to include in the token (optional)</param>
    /// <param name="expiresInMinutes">Token expiration time in minutes (default: 60)</param>
    /// <returns>A signed JWT token string</returns>
    public string SignToken(Dictionary<string, string>? claims = null, int expiresInSeconds = 180)
    {
        // Create a list of claims
        var claimsList = new List<Claim>
        {
            new("lxm", "com.atproto.server.createAccount"),
            new(JwtRegisteredClaimNames.Iss, _issuer),
            new(JwtRegisteredClaimNames.Aud, _audience),
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
        
        bool isHexFormat = !_privateKey.Contains("-----BEGIN");
        
        try
        {
            if (isHexFormat)
            {
                // Hex format - raw key bytes, assume ECDSA P-256
                var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var privateKeyBytes = Convert.FromHexString(_privateKey);
                var publicKeyBytes = Convert.FromHexString(_publicKey);
                
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
                    rsa.ImportFromPem(_privateKey);
                    var rsaKey = new RsaSecurityKey(rsa.ExportParameters(true));
                    signingCredentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
                }
                catch
                {
                    // Try ECDSA
                    var ecdsa = ECDsa.Create();
                    ecdsa.ImportFromPem(_privateKey);
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

