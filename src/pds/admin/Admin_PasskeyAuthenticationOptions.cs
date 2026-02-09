using System.Security.Cryptography;
using System.Text.Json.Nodes;
using dnproto.auth;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// Returns WebAuthn authentication options for passkey login.
/// </summary>
public class Admin_PasskeyAuthenticationOptions : BaseAdmin
{
    public IResult GetResponse()
    {
        IncrementStatistics();
        
        if (AdminInterfaceIsEnabled() == false)
        {
            return Results.StatusCode(404);
        }

        if (PasskeysEnabled() == false)
        {
            return Results.StatusCode(404);
        }

        //
        // Note: No auth required - this is called before login
        //

        //
        // Check if any passkeys exist
        //
        var existingPasskeys = Pds.PdsDb.GetAllPasskeys();
        if (existingPasskeys.Count == 0)
        {
            return Results.Json(new { error = "No passkeys registered" }, statusCode: 400);
        }

        //
        // Generate challenge (32 random bytes)
        //
        byte[] challengeBytes = new byte[32];
        RandomNumberGenerator.Fill(challengeBytes);
        string challenge = PasskeyUtils.Base64UrlEncode(challengeBytes);

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
        // Build allowCredentials from existing passkeys
        //
        var allowCredentials = new JsonArray();
        foreach (var passkey in existingPasskeys)
        {
            allowCredentials.Add(new JsonObject
            {
                ["type"] = "public-key",
                ["id"] = passkey.CredentialId
            });
        }

        //
        // Build authentication options
        //
        var options = new JsonObject
        {
            ["challenge"] = challenge,
            ["rpId"] = Pds.PdsDb.GetConfigProperty("PdsHostname"),
            ["timeout"] = 60000,
            ["userVerification"] = "preferred",
            ["allowCredentials"] = allowCredentials
        };

        return Results.Json(options);
    }
}
