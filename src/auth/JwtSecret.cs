using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel;

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


    #region OAUTH / DPOP

    /// <summary>
    /// Result of DPoP validation containing the parsed claims and any error.
    /// </summary>
    public class DPopValidationResult
    {
        public bool IsValid => string.IsNullOrEmpty(Error);
        public string? Error { get; set; }
        
        /// <summary>
        /// Debug info for troubleshooting signature verification failures.
        /// </summary>
        public string? DebugInfo { get; set; }
        
        /// <summary>
        /// The JWK thumbprint (SHA-256 hash of canonical JWK, base64url encoded).
        /// Used to bind tokens to this specific key.
        /// </summary>
        public string? JwkThumbprint { get; set; }
        
        /// <summary>
        /// The unique token identifier (jti) - should be stored to prevent replay.
        /// </summary>
        public string? Jti { get; set; }
        
        /// <summary>
        /// The HTTP method (htm) from the proof.
        /// </summary>
        public string? Htm { get; set; }
        
        /// <summary>
        /// The HTTP URI (htu) from the proof.
        /// </summary>
        public string? Htu { get; set; }
        
        /// <summary>
        /// The issued-at timestamp (iat) from the proof.
        /// </summary>
        public long? Iat { get; set; }
    }

    /// <summary>
    /// Validates a DPoP (Demonstrating Proof of Possession) proof.
    /// See RFC 9449: https://datatracker.ietf.org/doc/html/rfc9449
    /// </summary>
    /// <param name="dpop">The DPoP JWT from the DPoP header</param>
    /// <param name="expectedMethod">Expected HTTP method (e.g., "POST")</param>
    /// <param name="expectedUri">Expected HTTP URI (e.g., "https://pds.example.com/oauth/par")</param>
    /// <param name="maxAgeSeconds">Maximum age of the proof in seconds (default: 300)</param>
    /// <returns>Validation result with parsed claims or error</returns>
    public static DPopValidationResult ValidateDpop(
        string? dpop,
        string expectedMethod,
        string expectedUri,
        int maxAgeSeconds = 300)
    {
        var result = new DPopValidationResult();

        if (string.IsNullOrWhiteSpace(dpop))
        {
            result.Error = "DPoP header is missing";
            return result;
        }

        try
        {
            // Split JWT into parts
            var parts = dpop.Split('.');
            if (parts.Length != 3)
            {
                result.Error = "DPoP proof is not a valid JWT (expected 3 parts)";
                return result;
            }

            // Decode header
            var headerJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[0]));
            var header = System.Text.Json.JsonDocument.Parse(headerJson).RootElement;

            // Validate typ
            if (!header.TryGetProperty("typ", out var typElement) || typElement.GetString() != "dpop+jwt")
            {
                result.Error = $"Invalid DPoP typ: expected 'dpop+jwt'";
                return result;
            }

            // Validate alg (must be asymmetric)
            if (!header.TryGetProperty("alg", out var algElement))
            {
                result.Error = "DPoP header missing 'alg'";
                return result;
            }
            var alg = algElement.GetString();
            if (!IsAllowedDpopAlgorithm(alg))
            {
                result.Error = $"Invalid or unsupported DPoP algorithm: '{alg}'";
                return result;
            }

            // Extract JWK from header
            if (!header.TryGetProperty("jwk", out var jwkElement))
            {
                result.Error = "DPoP header missing 'jwk'";
                return result;
            }

            // Decode payload
            var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));
            var payload = System.Text.Json.JsonDocument.Parse(payloadJson).RootElement;

            // Extract and validate jti
            if (!payload.TryGetProperty("jti", out var jtiElement))
            {
                result.Error = "DPoP payload missing 'jti'";
                return result;
            }
            result.Jti = jtiElement.GetString();

            // Extract and validate htm
            if (!payload.TryGetProperty("htm", out var htmElement))
            {
                result.Error = "DPoP payload missing 'htm'";
                return result;
            }
            result.Htm = htmElement.GetString();
            if (!string.Equals(result.Htm, expectedMethod, StringComparison.OrdinalIgnoreCase))
            {
                result.Error = $"DPoP htm mismatch: expected '{expectedMethod}', got '{result.Htm}'";
                return result;
            }

            // Extract and validate htu
            if (!payload.TryGetProperty("htu", out var htuElement))
            {
                result.Error = "DPoP payload missing 'htu'";
                return result;
            }
            result.Htu = htuElement.GetString();
            if (!CompareDpopHtu(result.Htu, expectedUri))
            {
                result.Error = $"DPoP htu mismatch: expected '{expectedUri}', got '{result.Htu}'";
                return result;
            }

            // Extract and validate iat
            if (!payload.TryGetProperty("iat", out var iatElement))
            {
                result.Error = "DPoP payload missing 'iat'";
                return result;
            }
            result.Iat = iatElement.GetInt64();

            // Check iat timing
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (result.Iat > now + 60) // Allow 60 seconds clock skew
            {
                result.Error = $"DPoP iat is in the future";
                return result;
            }
            if (now - result.Iat > maxAgeSeconds)
            {
                result.Error = $"DPoP proof is too old ({now - result.Iat} seconds)";
                return result;
            }

            // Verify signature using embedded JWK
            var (sigValid, debugInfo) = VerifyDpopSignatureWithDebug(dpop, alg!, jwkElement);
            result.DebugInfo = debugInfo;
            if (!sigValid)
            {
                result.Error = "DPoP signature verification failed";
                return result;
            }

            // Calculate JWK thumbprint (RFC 7638)
            result.JwkThumbprint = CalculateDpopJwkThumbprint(jwkElement);

            return result;
        }
        catch (Exception ex)
        {
            result.Error = $"DPoP validation error: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Simple validation that just checks if DPoP is structurally valid.
    /// For full validation with method/URI checking, use ValidateDpop().
    /// </summary>
    public static bool IsValidDpop(string dpop)
    {
        if (string.IsNullOrEmpty(dpop))
        {
            return false;
        }

        try
        {
            var parts = dpop.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            // Decode and check header has required fields
            var headerJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[0]));
            var header = System.Text.Json.JsonDocument.Parse(headerJson).RootElement;

            if (!header.TryGetProperty("typ", out var typ) || typ.GetString() != "dpop+jwt")
            {
                return false;
            }

            if (!header.TryGetProperty("alg", out var alg) || !IsAllowedDpopAlgorithm(alg.GetString()))
            {
                return false;
            }

            if (!header.TryGetProperty("jwk", out _))
            {
                return false;
            }

            // Decode and check payload has required fields
            var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));
            var payload = System.Text.Json.JsonDocument.Parse(payloadJson).RootElement;

            if (!payload.TryGetProperty("jti", out _) ||
                !payload.TryGetProperty("htm", out _) ||
                !payload.TryGetProperty("htu", out _) ||
                !payload.TryGetProperty("iat", out _))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if algorithm is allowed for DPoP (asymmetric only).
    /// </summary>
    private static bool IsAllowedDpopAlgorithm(string? alg)
    {
        return alg switch
        {
            "RS256" or "RS384" or "RS512" => true,
            "PS256" or "PS384" or "PS512" => true,
            "ES256" or "ES256K" or "ES384" or "ES512" => true,
            _ => false
        };
    }

    /// <summary>
    /// Compare htu values, ignoring query string and fragment per RFC 9449.
    /// </summary>
    private static bool CompareDpopHtu(string? htu, string expectedUri)
    {
        if (string.IsNullOrEmpty(htu)) return false;

        try
        {
            var htuUri = new Uri(htu);
            var expectedUriObj = new Uri(expectedUri);

            return htuUri.Scheme.Equals(expectedUriObj.Scheme, StringComparison.OrdinalIgnoreCase) &&
                   htuUri.Host.Equals(expectedUriObj.Host, StringComparison.OrdinalIgnoreCase) &&
                   htuUri.Port == expectedUriObj.Port &&
                   htuUri.AbsolutePath.Equals(expectedUriObj.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(htu, expectedUri, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Verifies DPoP JWT signature using embedded JWK.
    /// </summary>
    private static bool VerifyDpopSignature(string jwt, string alg, System.Text.Json.JsonElement jwk)
    {
        try
        {
            var parts = jwt.Split('.');
            var signedData = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
            var signature = Base64UrlEncoder.DecodeBytes(parts[2]);

            if (!jwk.TryGetProperty("kty", out var ktyElement))
            {
                return false;
            }

            var kty = ktyElement.GetString();

            if (kty == "EC")
            {
                return VerifyEcDpopSignature(signedData, signature, alg, jwk);
            }
            else if (kty == "RSA")
            {
                return VerifyRsaDpopSignature(signedData, signature, alg, jwk);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifies DPoP JWT signature with debug info.
    /// </summary>
    private static (bool success, string debugInfo) VerifyDpopSignatureWithDebug(string jwt, string alg, System.Text.Json.JsonElement jwk)
    {
        var sb = new StringBuilder();
        try
        {
            var parts = jwt.Split('.');
            var signedData = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
            var signature = Base64UrlEncoder.DecodeBytes(parts[2]);

            sb.AppendLine($"alg={alg}");
            sb.AppendLine($"signedData.Length={signedData.Length}");
            sb.AppendLine($"signature.Length={signature.Length}");

            if (!jwk.TryGetProperty("kty", out var ktyElement))
            {
                sb.AppendLine("ERROR: missing kty");
                return (false, sb.ToString());
            }

            var kty = ktyElement.GetString();
            sb.AppendLine($"kty={kty}");

            if (kty == "EC")
            {
                // Extract curve info
                string? crv = null;
                if (jwk.TryGetProperty("crv", out var crvElement))
                {
                    crv = crvElement.GetString();
                }
                sb.AppendLine($"crv={crv}");

                if (jwk.TryGetProperty("x", out var xEl))
                {
                    var xVal = xEl.GetString();
                    var xBytes = Base64UrlEncoder.DecodeBytes(xVal!);
                    sb.AppendLine($"x.Length={xBytes.Length}");
                }
                if (jwk.TryGetProperty("y", out var yEl))
                {
                    var yVal = yEl.GetString();
                    var yBytes = Base64UrlEncoder.DecodeBytes(yVal!);
                    sb.AppendLine($"y.Length={yBytes.Length}");
                }

                var result = VerifyEcDpopSignature(signedData, signature, alg, jwk);
                sb.AppendLine($"EC verify result={result}");
                return (result, sb.ToString());
            }
            else if (kty == "RSA")
            {
                var result = VerifyRsaDpopSignature(signedData, signature, alg, jwk);
                sb.AppendLine($"RSA verify result={result}");
                return (result, sb.ToString());
            }

            sb.AppendLine($"ERROR: unsupported kty={kty}");
            return (false, sb.ToString());
        }
        catch (Exception ex)
        {
            sb.AppendLine($"EXCEPTION: {ex.Message}");
            return (false, sb.ToString());
        }
    }

    /// <summary>
    /// Verifies EC signature (ES256, ES384, ES512).
    /// </summary>
    private static bool VerifyEcDpopSignature(byte[] signedData, byte[] signature, string alg, System.Text.Json.JsonElement jwk)
    {
        if (!jwk.TryGetProperty("x", out var xElement) || !jwk.TryGetProperty("y", out var yElement))
        {
            return false;
        }

        var x = xElement.GetString();
        var y = yElement.GetString();
        if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y))
        {
            return false;
        }

        // Get curve from JWK (more reliable than inferring from alg)
        string? crv = null;
        if (jwk.TryGetProperty("crv", out var crvElement))
        {
            crv = crvElement.GetString();
        }

        ECCurve curve;
        HashAlgorithmName hashAlgorithm;
        int expectedKeySize;

        // Use crv from JWK if available, otherwise fall back to alg
        var curveIdentifier = crv ?? alg;
        switch (curveIdentifier)
        {
            case "P-256":
            case "ES256":
                curve = ECCurve.NamedCurves.nistP256;
                hashAlgorithm = HashAlgorithmName.SHA256;
                expectedKeySize = 32;
                break;
            case "P-384":
            case "ES384":
                curve = ECCurve.NamedCurves.nistP384;
                hashAlgorithm = HashAlgorithmName.SHA384;
                expectedKeySize = 48;
                break;
            case "P-521":
            case "ES512":
                curve = ECCurve.NamedCurves.nistP521;
                hashAlgorithm = HashAlgorithmName.SHA512;
                expectedKeySize = 66;
                break;
            case "secp256k1":
            case "ES256K":
                // secp256k1 (Bitcoin/Ethereum curve) - OID 1.3.132.0.10
                curve = ECCurve.CreateFromOid(new System.Security.Cryptography.Oid("1.3.132.0.10"));
                hashAlgorithm = HashAlgorithmName.SHA256;
                expectedKeySize = 32;
                break;
            default:
                return false;
        }

        var xBytes = PadDpopKeyBytes(Base64UrlEncoder.DecodeBytes(x), expectedKeySize);
        var yBytes = PadDpopKeyBytes(Base64UrlEncoder.DecodeBytes(y), expectedKeySize);

        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = curve,
            Q = new ECPoint { X = xBytes, Y = yBytes }
        });

        return ecdsa.VerifyData(signedData, signature, hashAlgorithm, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <summary>
    /// Verifies RSA signature (RS256, PS256, etc.).
    /// </summary>
    private static bool VerifyRsaDpopSignature(byte[] signedData, byte[] signature, string alg, System.Text.Json.JsonElement jwk)
    {
        if (!jwk.TryGetProperty("n", out var nElement) || !jwk.TryGetProperty("e", out var eElement))
        {
            return false;
        }

        var n = nElement.GetString();
        var e = eElement.GetString();
        if (string.IsNullOrEmpty(n) || string.IsNullOrEmpty(e))
        {
            return false;
        }

        using var rsa = RSA.Create(new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(n),
            Exponent = Base64UrlEncoder.DecodeBytes(e)
        });

        HashAlgorithmName hashAlgorithm;
        RSASignaturePadding padding;

        switch (alg)
        {
            case "RS256":
                hashAlgorithm = HashAlgorithmName.SHA256;
                padding = RSASignaturePadding.Pkcs1;
                break;
            case "RS384":
                hashAlgorithm = HashAlgorithmName.SHA384;
                padding = RSASignaturePadding.Pkcs1;
                break;
            case "RS512":
                hashAlgorithm = HashAlgorithmName.SHA512;
                padding = RSASignaturePadding.Pkcs1;
                break;
            case "PS256":
                hashAlgorithm = HashAlgorithmName.SHA256;
                padding = RSASignaturePadding.Pss;
                break;
            case "PS384":
                hashAlgorithm = HashAlgorithmName.SHA384;
                padding = RSASignaturePadding.Pss;
                break;
            case "PS512":
                hashAlgorithm = HashAlgorithmName.SHA512;
                padding = RSASignaturePadding.Pss;
                break;
            default:
                return false;
        }

        return rsa.VerifyData(signedData, signature, hashAlgorithm, padding);
    }

    /// <summary>
    /// Pads byte array to expected size (prepends zeros).
    /// </summary>
    private static byte[] PadDpopKeyBytes(byte[] data, int size)
    {
        if (data.Length >= size) return data;
        var padded = new byte[size];
        Array.Copy(data, 0, padded, size - data.Length, data.Length);
        return padded;
    }

    /// <summary>
    /// Calculates JWK thumbprint per RFC 7638.
    /// </summary>
    public static string CalculateDpopJwkThumbprint(System.Text.Json.JsonElement jwk)
    {
        if (!jwk.TryGetProperty("kty", out var ktyElement))
        {
            throw new InvalidOperationException("JWK missing 'kty'");
        }

        var kty = ktyElement.GetString();
        string canonicalJson;

        if (kty == "EC")
        {
            var crv = jwk.GetProperty("crv").GetString();
            var x = jwk.GetProperty("x").GetString();
            var y = jwk.GetProperty("y").GetString();
            canonicalJson = $"{{\"crv\":\"{crv}\",\"kty\":\"EC\",\"x\":\"{x}\",\"y\":\"{y}\"}}";
        }
        else if (kty == "RSA")
        {
            var e = jwk.GetProperty("e").GetString();
            var n = jwk.GetProperty("n").GetString();
            canonicalJson = $"{{\"e\":\"{e}\",\"kty\":\"RSA\",\"n\":\"{n}\"}}";
        }
        else
        {
            throw new InvalidOperationException($"Unsupported key type: {kty}");
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Base64UrlEncoder.Encode(hash);
    }

    #endregion
    
}