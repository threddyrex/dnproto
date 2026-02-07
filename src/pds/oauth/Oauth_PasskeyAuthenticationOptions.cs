using System.Security.Cryptography;
using System.Text.Json.Nodes;
using dnproto.auth;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;

/// <summary>
/// Returns WebAuthn authentication options for OAuth passkey login.
/// </summary>
public class Oauth_PasskeyAuthenticationOptions : BaseXrpcCommand
{
    public async Task<IResult> GetResponse()
    {
        if (!IsOauthEnabled())
        {
            return Results.Json(new{}, statusCode: 403);
        }

        IncrementStatistics();

        if (PasskeysEnabled() == false)
        {
            return Results.Json(new { error = "Passkeys not enabled" }, statusCode: 404);
        }

        //
        // Parse request body to get request_uri and client_id
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

        string? requestUri = json?["request_uri"]?.GetValue<string>();
        string? clientId = json?["client_id"]?.GetValue<string>();

        if (string.IsNullOrEmpty(requestUri) || string.IsNullOrEmpty(clientId))
        {
            return Results.Json(new { error = "Missing request_uri or client_id" }, statusCode: 400);
        }

        //
        // Verify OAuth request exists
        //
        if (!Pds.PdsDb.OauthRequestExists(requestUri))
        {
            Pds.Logger.LogWarning($"[OAUTH] [PASSKEY] OAuth request does not exist or has expired. request_uri={requestUri}");
            return Results.Json(new { error = "Invalid or expired OAuth request" }, statusCode: 400);
        }

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
            ["rpId"] = Pds.Config.PdsHostname,
            ["timeout"] = 60000,
            ["userVerification"] = "preferred",
            ["allowCredentials"] = allowCredentials
        };

        return Results.Json(options);
    }
}
