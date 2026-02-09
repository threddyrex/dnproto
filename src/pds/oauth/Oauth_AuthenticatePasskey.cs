using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using dnproto.auth;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;

/// <summary>
/// Authenticates a user via passkey (WebAuthn assertion verification) for OAuth flow.
/// Returns a redirect URL with authorization code instead of setting a session cookie.
/// </summary>
public class Oauth_AuthenticatePasskey : BaseXrpcCommand
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

        //
        // Get OAuth-specific parameters
        //
        string? requestUri = json["request_uri"]?.GetValue<string>();
        string? clientId = json["client_id"]?.GetValue<string>();

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

        OauthRequest oauthRequest = Pds.PdsDb.GetOauthRequest(requestUri);

        //
        // Get WebAuthn assertion data
        //
        string? credentialId = json["id"]?.GetValue<string>();
        string? clientDataJsonB64 = json["response"]?["clientDataJSON"]?.GetValue<string>();
        string? authenticatorDataB64 = json["response"]?["authenticatorData"]?.GetValue<string>();
        string? signatureB64 = json["response"]?["signature"]?.GetValue<string>();

        if (string.IsNullOrEmpty(credentialId) || string.IsNullOrEmpty(clientDataJsonB64) ||
            string.IsNullOrEmpty(authenticatorDataB64) || string.IsNullOrEmpty(signatureB64))
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

        if (type != "webauthn.get")
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
                Pds.PdsDb.DeletePasskeyChallenge(challenge!);
                return Results.Json(new { error = "Challenge expired" }, statusCode: 400);
            }
        }


        //
        // Validate origin
        //
        string expectedOrigin = PasskeyUtils.GetExpectedOrigin(Pds.PdsDb.GetConfigProperty("PdsHostname"), Pds.PdsDb.GetConfigPropertyInt("ServerListenPort"));
        if (origin != expectedOrigin)
        {
            return Results.Json(new { error = $"Invalid origin. Expected {expectedOrigin}, got {origin}" }, statusCode: 400);
        }


        //
        // Look up passkey by credential ID
        //
        Passkey passkey;
        try
        {
            passkey = Pds.PdsDb.GetPasskeyByCredentialId(credentialId);
        }
        catch
        {
            return Results.Json(new { error = "Unknown credential" }, statusCode: 400);
        }


        //
        // Verify the signature
        // Signature is over: authenticatorData || SHA256(clientDataJSON)
        //
        byte[] authenticatorData = PasskeyUtils.Base64UrlDecode(authenticatorDataB64);
        byte[] signature = PasskeyUtils.Base64UrlDecode(signatureB64);

        //
        // Validate authenticatorData structure
        //
        if (!PasskeyUtils.ValidateAuthenticatorData(authenticatorData, Pds.PdsDb.GetConfigProperty("PdsHostname"), out string? authDataError))
        {
            Pds.Logger.LogWarning($"[AUTH] [OAUTH] [PASSKEY] {authDataError} for credential {credentialId}");
            return Results.Json(new { error = authDataError }, statusCode: 400);
        }

        // Build the signed data
        byte[] signedData = PasskeyUtils.BuildSignedData(authenticatorData, clientDataJsonBytes);

        // Parse COSE key and verify signature
        byte[] publicKeyBytes = PasskeyUtils.Base64UrlDecode(passkey.PublicKey);
        bool signatureValid;
        try
        {
            signatureValid = PasskeyUtils.VerifyCoseSignature(publicKeyBytes, signedData, signature);
        }
        catch (Exception ex)
        {
            Pds.Logger.LogWarning($"[AUTH] [OAUTH] [PASSKEY] Signature verification failed: {ex.Message}");
            return Results.Json(new { error = "Signature verification failed" }, statusCode: 400);
        }

        if (!signatureValid)
        {
            Pds.Logger.LogWarning($"[AUTH] [OAUTH] [PASSKEY] Invalid signature for credential {credentialId}");
            return Results.Json(new { error = "Invalid signature" }, statusCode: 401);
        }


        //
        // Delete used challenge
        //
        Pds.PdsDb.DeletePasskeyChallenge(challenge!);


        //
        // Generate authorization code and update the oauth request
        //
        string authorizationCode = "authcode-" + Guid.NewGuid().ToString();
        oauthRequest.AuthorizationCode = authorizationCode;
        oauthRequest.AuthType = "Passkey";
        Pds.PdsDb.UpdateOauthRequest(oauthRequest);


        //
        // Build redirect URL
        //
        string redirectUri = XrpcHelpers.GetRequestBodyArgumentValue(oauthRequest.Body, "redirect_uri");
        string state = XrpcHelpers.GetRequestBodyArgumentValue(oauthRequest.Body, "state");
        string issuer = $"https://{Pds.PdsDb.GetConfigProperty("PdsHostname")}";

        string redirectUrl = $"{redirectUri}?code={Uri.EscapeDataString(authorizationCode)}&state={Uri.EscapeDataString(state)}&iss={Uri.EscapeDataString(issuer)}";

        Pds.Logger.LogInfo($"[AUTH] [OAUTH] [PASSKEY] authSucceeded=true passkey={passkey.Name} redirect_url={redirectUrl}");

        return Results.Json(new { success = true, redirect_url = redirectUrl });
    }
}
