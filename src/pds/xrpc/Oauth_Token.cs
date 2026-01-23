using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class Oauth_Token : BaseXrpcCommand
{
    public async Task<IResult> GetResponse()
    {
        //
        // Check if OAuth is enabled
        //
        if(!IsOauthEnabled())
        {
            return Results.Json(new{}, statusCode: 403);
        }


        //
        // Retrieve body params
        //
        var body = await HttpContext.Request.ReadFromJsonAsync<JsonObject>();
        if(body is null)
        {
            return Results.Json(new{}, statusCode: 400);
        }


        //
        // grant_type: authorization_code
        //
        string? grantType = body["grant_type"]?.GetValue<string>();
        if(string.Equals(grantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
        {
            string? code = body["code"]?.GetValue<string>();
            string? codeVerifier = body["code_verifier"]?.GetValue<string>();
            string? redirectUri = body["redirect_uri"]?.GetValue<string>();
            string? clientId = body["client_id"]?.GetValue<string>();

            if(string.IsNullOrEmpty(code) 
                || string.IsNullOrEmpty(codeVerifier) 
                || string.IsNullOrEmpty(redirectUri) 
                || string.IsNullOrEmpty(clientId))
            {
                Pds.Logger.LogWarning("[OAUTH] authorization_code. Invalid OAuth token request: missing required parameters");
                return Results.Json(new{}, statusCode: 400);
            }


            //
            // Validate dpop
            //
            string? dpopHeader = HttpContext.Request.Headers["DPoP"];
            if(string.IsNullOrEmpty(dpopHeader))
            {
                Pds.Logger.LogWarning($"[OAUTH] authorization_code. dpop is null.");
                return Results.Json(new{}, statusCode: 401);
            }
            var dpopResult = JwtSecret.ValidateDpop(
                dpopHeader, 
                "POST", 
                $"https://{Pds.Config.PdsHostname}/oauth/token");

            if(!dpopResult.IsValid || string.IsNullOrEmpty(dpopResult.JwkThumbprint))
            {
                Pds.Logger.LogWarning($"[OAUTH] authorization_code. dpop or thumbprint is invalid.");
                return Results.Json(new{}, statusCode: 401);
            }


            //
            // Look up oauth request
            //
            if(! Pds.PdsDb.OauthRequestExistsByAuthorizationCode(code))
            {
                Pds.Logger.LogWarning($"[OAUTH] authorization_code. Invalid OAuth token request: authorization code not found or expired. code={code}");
                return Results.Json(new{}, statusCode: 400);
            }

            OauthRequest oauthRequest = Pds.PdsDb.GetOauthRequestByAuthorizationCode(code);


            //
            // Verify code_verifier
            //
            string codeChallenge = XrpcHelpers.GetRequestBodyArgumentValue(oauthRequest.Body, "code_challenge");
            string computedChallenge = ComputeS256CodeChallenge(codeVerifier);
            if(!string.Equals(codeChallenge, computedChallenge, StringComparison.Ordinal))
            {
                Pds.Logger.LogWarning($"[OAUTH] authorization_code. Invalid OAuth token request: code_verifier does not match code_challenge. code={code}");
                return Results.Json(new{}, statusCode: 400);
            }

            //
            // Verify redirect_uri
            //
            if(!string.Equals(XrpcHelpers.GetRequestBodyArgumentValue(oauthRequest.Body, "redirect_uri"), redirectUri, StringComparison.Ordinal))
            {
                Pds.Logger.LogWarning($"[OAUTH] authorization_code. Invalid OAuth token request: redirect_uri does not match. code={code}");
                return Results.Json(new{}, statusCode: 400);
            }

            //
            // Verify client_id
            //
            if(!string.Equals(XrpcHelpers.GetRequestBodyArgumentValue(oauthRequest.Body, "client_id"), clientId, StringComparison.Ordinal))
            {
                Pds.Logger.LogWarning($"[OAUTH] authorization_code. Invalid OAuth token request: client_id does not match. code={code}");
                return Results.Json(new{}, statusCode: 400);
            }


            //
            // Create new OauthSession
            //
            var oauthSession = new OauthSession()
            {
                SessionId = "sessionid-" + Guid.NewGuid().ToString(),
                ClientId = clientId,
                Scope = XrpcHelpers.GetRequestBodyArgumentValue(oauthRequest.Body, "scope"),
                DpopJwkThumbprint = dpopResult.JwkThumbprint,
                RefreshToken = "refresh-" + Guid.NewGuid().ToString(),
                RefreshTokenExpiresDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow.AddDays(90)),
                CreatedDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow)
            };

            Pds.PdsDb.InsertOauthSession(oauthSession);
            Pds.Logger.LogInfo($"[OAUTH] authorization_code. Created new OAuth session: sessionId={oauthSession.SessionId}");


            //
            // Delete the OauthRequest (authorization code is single-use)
            //
            Pds.PdsDb.DeleteOauthRequestByAuthorizationCode(code);


            //
            // Generate access token
            //
            string issuer = $"https://{Pds.Config.PdsHostname}";
            int expiresInSeconds = 3600; // 1 hour

            string? accessToken = JwtSecret.GenerateOAuthAccessToken(
                Pds.Config.UserDid,
                issuer,
                oauthSession.Scope,
                oauthSession.DpopJwkThumbprint,
                Pds.Config.JwtSecret,
                clientId,
                expiresInSeconds);

            if(string.IsNullOrEmpty(accessToken))
            {
                Pds.Logger.LogError("[OAUTH] Failed to generate access token");
                return Results.Json(new{}, statusCode: 500);
            }

            Pds.Logger.LogInfo($"[OAUTH] authorization_code. Token issued. sessionId={oauthSession.SessionId} sub={Pds.Config.UserDid}");

            return Results.Json(new
            {
                access_token = accessToken,
                token_type = "DPoP",
                expires_in = expiresInSeconds,
                refresh_token = oauthSession.RefreshToken,
                scope = oauthSession.Scope,
                sub = Pds.Config.UserDid
            }, statusCode: 200);
        }
        //
        // grant_type: refresh_token
        //
        else if(string.Equals(grantType, "refresh_token", StringComparison.OrdinalIgnoreCase))
        {
            //
            // Get refresh_token from request body
            //
            string? refreshToken = body["refresh_token"]?.GetValue<string>();
            if(string.IsNullOrEmpty(refreshToken))
            {
                Pds.Logger.LogWarning($"[OAUTH] refresh_token. Invalid OAuth token request: missing refresh_token");
                return Results.Json(new{}, statusCode: 400);
            }

            //
            // Validate dpop
            //
            string? dpopHeader = HttpContext.Request.Headers["DPoP"];
            if(string.IsNullOrEmpty(dpopHeader))
            {
                Pds.Logger.LogWarning($"[OAUTH] refresh_token. dpop is null.");
                return Results.Json(new{}, statusCode: 401);
            }
            var dpopResult = JwtSecret.ValidateDpop(
                dpopHeader, 
                "POST", 
                $"https://{Pds.Config.PdsHostname}/oauth/token");

            if(!dpopResult.IsValid || string.IsNullOrEmpty(dpopResult.JwkThumbprint))
            {
                Pds.Logger.LogWarning($"[OAUTH] refresh_token. dpop or thumbprint is invalid.");
                return Results.Json(new{}, statusCode: 401);
            }
            

            //
            // Look up the OAuth session by refresh token
            //
            if(! Pds.PdsDb.HasOauthSessionByRefreshToken(refreshToken))
            {
                Pds.Logger.LogWarning($"[OAUTH] refresh_token. OAuth session not found for refresh token: {refreshToken}");
                return Results.Json(new{}, statusCode: 401);
            }
            var oauthSession = Pds.PdsDb.GetOauthSessionByRefreshToken(refreshToken);


            //
            // Verify thumbprint matches
            //
            if(!string.Equals(oauthSession.DpopJwkThumbprint, dpopResult.JwkThumbprint, StringComparison.OrdinalIgnoreCase))
            {
                Pds.Logger.LogWarning($"[OAUTH] refresh_token. OAuth session thumbprint does not match: sessionId={oauthSession.SessionId}");
                return Results.Json(new{}, statusCode: 401);
            }

            //
            // Generate new refresh token
            //
            oauthSession.RefreshToken = "refresh-" + Guid.NewGuid().ToString();
            oauthSession.RefreshTokenExpiresDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow.AddDays(30));
            Pds.PdsDb.UpdateOauthSession(oauthSession);


            //
            // Generate access token
            //
            string issuer = $"https://{Pds.Config.PdsHostname}";
            int expiresInSeconds = 3600; // 1 hour

            string? accessToken = JwtSecret.GenerateOAuthAccessToken(
                Pds.Config.UserDid,
                issuer,
                oauthSession.Scope,
                oauthSession.DpopJwkThumbprint,
                Pds.Config.JwtSecret,
                oauthSession.ClientId,
                expiresInSeconds);

            if(string.IsNullOrEmpty(accessToken))
            {
                Pds.Logger.LogError("[OAUTH] Failed to generate access token");
                return Results.Json(new{}, statusCode: 500);
            }


            //
            // Return
            //
            return Results.Json(new
            {
                access_token = accessToken,
                token_type = "DPoP",
                expires_in = expiresInSeconds,
                refresh_token = oauthSession.RefreshToken,
                scope = oauthSession.Scope,
                sub = Pds.Config.UserDid
            },
            statusCode: 200);

        }
        //
        // Wrong
        //
        else
        {
            Pds.Logger.LogWarning($"[OAUTH] Invalid OAuth token request: unsupported grant type. grant_type={grantType}");
            return Results.Json(new{}, statusCode: 400);
        }
    }

    /// <summary>
    /// Computes the S256 code challenge from a code verifier.
    /// Returns BASE64URL(SHA256(code_verifier)) per RFC 7636.
    /// </summary>
    private static string ComputeS256CodeChallenge(string codeVerifier)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(codeVerifier);
        byte[] hash = SHA256.HashData(bytes);
        
        // Base64URL encode: standard base64, then replace +/ with -_, remove padding
        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}