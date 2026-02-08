using System.Formats.Cbor;
using System.Text;
using System.Text.Json.Nodes;
using dnproto.auth;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// Registers a new passkey after WebAuthn credential creation.
/// </summary>
public class Admin_RegisterPasskey : BaseAdmin
{
    public async Task<IResult> GetResponse()
    {
        IncrementStatistics();
        
        if (AdminInterfaceIsEnabled() == false)
        {
            return Results.StatusCode(404);
        }


        //
        // Require auth
        //
        if (AdminIsAuthenticated() == false)
        {
            return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
        }


        //
        // Parse request body
        //
        string requestBody;
        using (var reader = new StreamReader(HttpContext.Request.Body))
        {
            requestBody = await reader.ReadToEndAsync();
        }

        JsonNode? json;
        try
        {
            json = JsonNode.Parse(requestBody);
        }
        catch
        {
            return Results.Json(new { error = "Invalid JSON" }, statusCode: 400);
        }

        if (json == null)
        {
            return Results.Json(new { error = "Empty request body" }, statusCode: 400);
        }

        string? name = json["name"]?.GetValue<string>();
        string? credentialId = json["id"]?.GetValue<string>();
        string? clientDataJsonB64 = json["response"]?["clientDataJSON"]?.GetValue<string>();
        string? attestationObjectB64 = json["response"]?["attestationObject"]?.GetValue<string>();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(credentialId) ||
            string.IsNullOrEmpty(clientDataJsonB64) || string.IsNullOrEmpty(attestationObjectB64))
        {
            return Results.Json(new { error = "Missing required fields" }, statusCode: 400);
        }


        //
        // Decode and validate clientDataJSON
        //
        byte[] clientDataJsonBytes = PasskeyUtils.Base64UrlDecode(clientDataJsonB64);
        string clientDataJsonStr = Encoding.UTF8.GetString(clientDataJsonBytes);
        JsonNode? clientData = JsonNode.Parse(clientDataJsonStr);

        if (clientData == null)
        {
            return Results.Json(new { error = "Invalid clientDataJSON" }, statusCode: 400);
        }

        string? type = clientData["type"]?.GetValue<string>();
        string? challenge = clientData["challenge"]?.GetValue<string>();
        string? origin = clientData["origin"]?.GetValue<string>();

        if (type != "webauthn.create")
        {
            return Results.Json(new { error = "Invalid ceremony type" }, statusCode: 400);
        }

        //
        // Validate challenge exists in database
        //
        var storedChallenge = Pds.PdsDb.GetPasskeyChallenge(challenge ?? "");
        if (storedChallenge == null)
        {
            return Results.Json(new { error = "Invalid or expired challenge" }, statusCode: 400);
        }

        // Check challenge is not too old (5 minutes)
        if (DateTimeOffset.TryParse(storedChallenge.CreatedDate, out DateTimeOffset createdDate))
        {
            if (DateTimeOffset.UtcNow - createdDate > TimeSpan.FromMinutes(5))
            {
                return Results.Json(new { error = "Challenge expired" }, statusCode: 400);
            }
        }

        //
        // Validate origin
        // In development (localhost), include the port. In production, a reverse proxy handles SSL on 443.
        //
        string expectedOrigin = PasskeyUtils.GetExpectedOrigin(Pds.Config.PdsHostname, Pds.PdsDb.GetConfigPropertyInt("ServerListenPort"));
        if (origin != expectedOrigin)
        {
            return Results.Json(new { error = $"Invalid origin. Expected {expectedOrigin}, got {origin}" }, statusCode: 400);
        }


        //
        // Decode attestationObject and extract public key
        //
        byte[] attestationObjectBytes = PasskeyUtils.Base64UrlDecode(attestationObjectB64);
        string? publicKeyB64;

        try
        {
            publicKeyB64 = ExtractPublicKeyFromAttestationObject(attestationObjectBytes);
        }
        catch (Exception ex)
        {
            return Results.Json(new { error = $"Failed to extract public key: {ex.Message}" }, statusCode: 400);
        }

        if (string.IsNullOrEmpty(publicKeyB64))
        {
            return Results.Json(new { error = "Could not extract public key" }, statusCode: 400);
        }


        //
        // Check if passkey with same name already exists
        //
        var existingPasskeys = Pds.PdsDb.GetAllPasskeys();
        if (existingPasskeys.Any(p => p.Name == name))
        {
            return Results.Json(new { error = "A passkey with this name already exists" }, statusCode: 400);
        }


        //
        // Store the passkey
        //
        var passkey = new Passkey
        {
            Name = name,
            CredentialId = credentialId,
            PublicKey = publicKeyB64,
            CreatedDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow)
        };

        Pds.PdsDb.InsertPasskey(passkey);


        //
        // Delete used challenge
        //
        Pds.PdsDb.DeletePasskeyChallenge(challenge!);


        return Results.Redirect("/admin/");
    }


    /// <summary>
    /// Extracts the COSE public key from the attestationObject and returns it as base64url.
    /// The attestationObject is CBOR-encoded and contains authData which has the public key.
    /// </summary>
    private static string ExtractPublicKeyFromAttestationObject(byte[] attestationObject)
    {
        // Decode the CBOR attestation object
        var reader = new CborReader(attestationObject);
        reader.ReadStartMap();

        byte[]? authData = null;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            string key = reader.ReadTextString();
            if (key == "authData")
            {
                authData = reader.ReadByteString();
            }
            else
            {
                reader.SkipValue();
            }
        }

        if (authData == null)
        {
            throw new Exception("authData not found in attestation object");
        }

        // Parse authData structure:
        // - 32 bytes: rpIdHash
        // - 1 byte: flags
        // - 4 bytes: signCount
        // - variable: attestedCredentialData (if AT flag is set)

        if (authData.Length < 37)
        {
            throw new Exception("authData too short");
        }

        byte flags = authData[32];
        bool hasAttestedCredentialData = (flags & 0x40) != 0; // AT flag

        if (!hasAttestedCredentialData)
        {
            throw new Exception("No attested credential data in authData");
        }

        // Skip rpIdHash (32) + flags (1) + signCount (4) = 37 bytes
        int offset = 37;

        // attestedCredentialData:
        // - 16 bytes: AAGUID
        // - 2 bytes: credentialIdLength (big-endian)
        // - credentialIdLength bytes: credentialId
        // - variable: credentialPublicKey (COSE key)

        if (authData.Length < offset + 18)
        {
            throw new Exception("authData too short for attested credential data");
        }

        // Skip AAGUID
        offset += 16;

        // Read credential ID length
        int credIdLength = (authData[offset] << 8) | authData[offset + 1];
        offset += 2;

        // Skip credential ID
        offset += credIdLength;

        // The rest is the COSE public key
        int publicKeyLength = authData.Length - offset;
        byte[] publicKey = new byte[publicKeyLength];
        Array.Copy(authData, offset, publicKey, 0, publicKeyLength);

        return PasskeyUtils.Base64UrlEncode(publicKey);
    }
}