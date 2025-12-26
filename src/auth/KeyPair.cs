using System.Security.Cryptography;
using dnproto.repo;

namespace dnproto.auth;

public class KeyPair
{
    public required string KeyType { get; set; }
    public required string KeyTypeName { get; set; }
    public required string PublicKeyMultibase { get; set; }
    public required string PublicKeyHex { get; set; }
    public required string PrivateKeyMultibase { get; set; }
    public required string PrivateKeyHex { get; set; }
    public required string DidKey { get; set; }

    /// <summary>
    /// Generates a new cryptographic key pair.
    /// </summary>
    /// <param name="keyType">The type of key to generate: "p256" (default) or "secp256k1"</param>
    /// <returns>A KeyPair object containing the generated key pair</returns>
    public static KeyPair Generate(string keyType)
    {

        byte[] privateKeyBytes;
        byte[] publicKeyBytes;
        string keyTypeName;
        byte privateKeyPrefix1, privateKeyPrefix2;
        byte publicKeyPrefix1, publicKeyPrefix2;

        if (keyType.ToLower() == KeyTypes.Secp256k1)
        {
            // Generate secp256k1 key pair
            using var ecdsa = ECDsa.Create(ECCurve.CreateFromFriendlyName("secp256k1"));
            ECParameters parameters = ecdsa.ExportParameters(true);
            
            privateKeyBytes = parameters.D!;
            
            // Compress the public key (33 bytes: 0x02/0x03 prefix + 32-byte X coordinate)
            publicKeyBytes = new byte[33];
            publicKeyBytes[0] = (byte)(parameters.Q.Y![parameters.Q.Y.Length - 1] % 2 == 0 ? 0x02 : 0x03);
            Array.Copy(parameters.Q.X!, 0, publicKeyBytes, 1, 32);

            keyTypeName = "K-256 / secp256k1 / ES256K private key";
            // ATProto/Bluesky multicodec prefixes for secp256k1
            privateKeyPrefix1 = 0x81;
            privateKeyPrefix2 = 0x26;
            publicKeyPrefix1 = 0xE7;
            publicKeyPrefix2 = 0x01;
        }
        else if (keyType.ToLower() == KeyTypes.P256)
        {
            // Generate P-256 key pair
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            ECParameters parameters = ecdsa.ExportParameters(true);
            
            privateKeyBytes = parameters.D!;
            
            // Compress the public key (33 bytes: 0x02/0x03 prefix + 32-byte X coordinate)
            publicKeyBytes = new byte[33];
            publicKeyBytes[0] = (byte)(parameters.Q.Y![parameters.Q.Y.Length - 1] % 2 == 0 ? 0x02 : 0x03);
            Array.Copy(parameters.Q.X!, 0, publicKeyBytes, 1, 32);

            keyTypeName = "P-256 / secp256r1 / ES256 private key";
            // ATProto/Bluesky multicodec prefixes for P-256
            privateKeyPrefix1 = 0x86;
            privateKeyPrefix2 = 0x26;
            publicKeyPrefix1 = 0x80;
            publicKeyPrefix2 = 0x24;
        }
        else
        {
            throw new ArgumentException($"Unsupported key type: {keyType}. Supported types are: {KeyTypes.P256}, {KeyTypes.Secp256k1}");
        }

        // Add multicodec prefix for private key
        byte[] privateKeyWithPrefix = new byte[privateKeyBytes.Length + 2];
        privateKeyWithPrefix[0] = privateKeyPrefix1;
        privateKeyWithPrefix[1] = privateKeyPrefix2;
        Array.Copy(privateKeyBytes, 0, privateKeyWithPrefix, 2, privateKeyBytes.Length);

        // Add multicodec prefix for public key
        byte[] publicKeyWithPrefix = new byte[publicKeyBytes.Length + 2];
        publicKeyWithPrefix[0] = publicKeyPrefix1;
        publicKeyWithPrefix[1] = publicKeyPrefix2;
        Array.Copy(publicKeyBytes, 0, publicKeyWithPrefix, 2, publicKeyBytes.Length);

        // Encode in multibase format (base58btc with 'z' prefix)
        string privateKeyMultibase = Base58BtcEncoding.EncodeMultibase(privateKeyWithPrefix);
        string publicKeyMultibase = Base58BtcEncoding.EncodeMultibase(publicKeyWithPrefix);

        // Convert keys to hex strings
        string privateKeyHex = Convert.ToHexString(privateKeyBytes).ToLowerInvariant();
        string publicKeyHex = Convert.ToHexString(publicKeyBytes).ToLowerInvariant();

        return new KeyPair
        {
            KeyType = keyType.ToLower(),
            KeyTypeName = keyTypeName,
            PublicKeyMultibase = publicKeyMultibase,
            PublicKeyHex = publicKeyHex,
            PrivateKeyMultibase = privateKeyMultibase,
            PrivateKeyHex = privateKeyHex,
            DidKey = $"did:key:z{Base58BtcEncoding.Encode(publicKeyWithPrefix)}"
        };
    }
}

public class KeyTypes
{
    public static readonly string P256 = "p256";
    public static readonly string Secp256k1 = "secp256k1";
}