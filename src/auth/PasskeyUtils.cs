using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;

namespace dnproto.auth;

/// <summary>
/// Shared utility methods for WebAuthn/Passkey operations.
/// Used by both admin passkey auth and OAuth passkey auth flows.
/// </summary>
public static class PasskeyUtils
{
    /// <summary>
    /// Decodes a base64url-encoded string to bytes.
    /// </summary>
    public static byte[] Base64UrlDecode(string base64url)
    {
        string base64 = base64url.Replace("-", "+").Replace("_", "/");
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }

    /// <summary>
    /// Encodes bytes to a base64url-encoded string.
    /// </summary>
    public static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>
    /// Verifies a WebAuthn signature using a COSE-encoded public key.
    /// Supports ES256 (alg=-7) and RS256 (alg=-257).
    /// </summary>
    public static bool VerifyCoseSignature(byte[] coseKey, byte[] data, byte[] signature)
    {
        var reader = new CborReader(coseKey);
        reader.ReadStartMap();

        int? kty = null;
        int? alg = null;
        int? crv = null;
        byte[]? x = null;
        byte[]? y = null;
        byte[]? n = null;
        byte[]? e = null;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            int key = reader.ReadInt32();
            switch (key)
            {
                case 1: // kty
                    kty = reader.ReadInt32();
                    break;
                case 3: // alg
                    alg = reader.ReadInt32();
                    break;
                case -1: // crv (for EC) or n (for RSA)
                    if (reader.PeekState() == CborReaderState.ByteString)
                        n = reader.ReadByteString();
                    else
                        crv = reader.ReadInt32();
                    break;
                case -2: // x (for EC) or e (for RSA)
                    if (reader.PeekState() == CborReaderState.ByteString)
                    {
                        var bytes = reader.ReadByteString();
                        if (kty == 2) x = bytes; // EC
                        else e = bytes; // RSA
                    }
                    else
                        reader.SkipValue();
                    break;
                case -3: // y (for EC)
                    y = reader.ReadByteString();
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }

        if (kty == 2) // EC2
        {
            if (x == null || y == null)
                throw new Exception("Missing EC key coordinates");

            // ES256 uses P-256 curve
            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = x, Y = y }
            });

            // WebAuthn signature is in DER format, need to convert if necessary
            // .NET's VerifyData with DSASignatureFormat.Rfc3279DerSequence handles DER
            return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        else if (kty == 3) // RSA
        {
            if (n == null || e == null)
                throw new Exception("Missing RSA key parameters");

            using var rsa = RSA.Create(new RSAParameters
            {
                Modulus = n,
                Exponent = e
            });

            return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        else
        {
            throw new Exception($"Unsupported key type: {kty}");
        }
    }

    /// <summary>
    /// Computes the expected origin for WebAuthn verification based on PDS configuration.
    /// </summary>
    public static string GetExpectedOrigin(string pdsHostname, int listenPort)
    {
        if (pdsHostname == "localhost")
        {
            return $"https://{pdsHostname}:{listenPort}";
        }
        return $"https://{pdsHostname}";
    }

    /// <summary>
    /// Validates the authenticator data from a WebAuthn assertion.
    /// Verifies rpIdHash and user presence flag.
    /// </summary>
    /// <param name="authenticatorData">The authenticator data bytes</param>
    /// <param name="rpId">The relying party ID (hostname)</param>
    /// <param name="error">Output error message if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateAuthenticatorData(byte[] authenticatorData, string rpId, out string? error)
    {
        error = null;

        // Structure: rpIdHash (32) + flags (1) + signCount (4) + optional extensions
        if (authenticatorData.Length < 37)
        {
            error = "Invalid authenticator data";
            return false;
        }

        // Verify rpIdHash matches expected rpId
        byte[] expectedRpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
        byte[] actualRpIdHash = authenticatorData[..32];
        if (!CryptographicOperations.FixedTimeEquals(expectedRpIdHash, actualRpIdHash))
        {
            error = "Invalid rpIdHash";
            return false;
        }

        // Verify User Present (UP) flag is set
        byte flags = authenticatorData[32];
        bool userPresent = (flags & 0x01) != 0;
        if (!userPresent)
        {
            error = "User presence required";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds the signed data for WebAuthn assertion verification.
    /// This is: authenticatorData || SHA256(clientDataJSON)
    /// </summary>
    public static byte[] BuildSignedData(byte[] authenticatorData, byte[] clientDataJsonBytes)
    {
        byte[] clientDataHash = SHA256.HashData(clientDataJsonBytes);
        byte[] signedData = new byte[authenticatorData.Length + clientDataHash.Length];
        Array.Copy(authenticatorData, 0, signedData, 0, authenticatorData.Length);
        Array.Copy(clientDataHash, 0, signedData, authenticatorData.Length, clientDataHash.Length);
        return signedData;
    }
}
