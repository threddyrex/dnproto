using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// Returns WebAuthn registration options for passkey creation.
/// </summary>
public class Admin_PasskeyRegistrationOptions : BaseAdmin
{
    public IResult GetResponse()
    {
        IncrementStatistics();
        
        if(AdminInterfaceIsEnabled() == false)
        {
            return Results.StatusCode(404);
        }


        //
        // Require auth
        //
        if(AdminIsAuthenticated() == false)
        {
            return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
        }


        //
        // Generate challenge (32 random bytes)
        //
        byte[] challengeBytes = new byte[32];
        RandomNumberGenerator.Fill(challengeBytes);
        string challenge = Base64UrlEncode(challengeBytes);

        //
        // Store challenge in database
        //
        var passkeyChallenge = new PasskeyChallenge
        {
            Challenge = challenge,
            CreatedDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow)
        };
        Pds.PdsDb.InsertPasskeyChallenge(passkeyChallenge);

        //
        // Get existing passkeys for excludeCredentials
        //
        var existingPasskeys = Pds.PdsDb.GetAllPasskeys();
        var excludeCredentials = new JsonArray();
        foreach (var passkey in existingPasskeys)
        {
            excludeCredentials.Add(new JsonObject
            {
                ["type"] = "public-key",
                ["id"] = passkey.CredentialId
            });
        }

        //
        // Build user ID from UserDid (hash it to get consistent opaque ID)
        //
        byte[] userIdBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Pds.Config.UserDid));
        string userId = Base64UrlEncode(userIdBytes);

        //
        // Build registration options
        //
        var options = new JsonObject
        {
            ["challenge"] = challenge,
            ["rp"] = new JsonObject
            {
                ["name"] = Pds.Config.PdsHostname,
                ["id"] = Pds.Config.PdsHostname
            },
            ["user"] = new JsonObject
            {
                ["id"] = userId,
                ["name"] = Pds.Config.UserHandle,
                ["displayName"] = Pds.Config.UserHandle
            },
            ["pubKeyCredParams"] = new JsonArray
            {
                new JsonObject { ["type"] = "public-key", ["alg"] = -7 },   // ES256
                new JsonObject { ["type"] = "public-key", ["alg"] = -257 }  // RS256
            },
            ["timeout"] = 60000,
            ["attestation"] = "none",
            ["authenticatorSelection"] = new JsonObject
            {
                ["residentKey"] = "preferred",
                ["userVerification"] = "preferred"
            },
            ["excludeCredentials"] = excludeCredentials
        };

        return Results.Json(options);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}