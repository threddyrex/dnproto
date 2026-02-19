using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.log;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class Oauth_Authorize_Post : BaseXrpcCommand
{
    public async Task<IResult> GetResponse()
    {
        if(!IsOauthEnabled())
        {
            return Results.Json(new{}, statusCode: 403);
        }

        IncrementStatistics();

        //
        // Get form data
        //
        string? body = HttpContext.Request.Body != null ? await new StreamReader(HttpContext.Request.Body).ReadToEndAsync() : null;
        if (string.IsNullOrEmpty(body))
        {
            Pds.Logger.LogWarning($"[OAUTH] Missing form data.");
            return Results.Json(new{}, statusCode: 401);
        }

        string? clientId = null, requestUri = null, userName = null, password = null;

        if(CheckFormDataParam(body, "client_id", out clientId) == false
            || CheckFormDataParam(body, "request_uri", out requestUri) == false
            || CheckFormDataParam(body, "username", out userName) == false
            || CheckFormDataParam(body, "password", out password) == false)
        {
            Pds.Logger.LogWarning($"[OAUTH] Missing parameters. client_id={clientId} request_uri={requestUri} username={userName}");
            return Results.Json(new{}, statusCode: 401);            
        }


        //
        // Load the oauth request
        //
        if(!Pds.PdsDb.OauthRequestExists(requestUri!))
        {
            Pds.Logger.LogWarning($"[OAUTH] Oauth request does not exist or has expired. request_uri={requestUri}");
            return Results.Json(new{}, statusCode: 401);
        }

        OauthRequest oauthRequest = Pds.PdsDb.GetOauthRequest(requestUri!);


        //
        // Resolve actor info and check password
        //
        ActorInfo? actorInfo = BlueskyClient.ResolveActorInfo(userName!);
        bool actorExists = actorInfo != null;
        string? storedHashedPassword = Pds.PdsDb.GetConfigProperty("UserHashedPassword");
        bool passwordMatches = PasswordHasher.VerifyPassword(storedHashedPassword, password);
        bool authSucceeded = actorExists && passwordMatches;

        if(authSucceeded == false)
        {
            Pds.Logger.LogWarning($"[OAUTH] Authentication failed. username={userName} actorExists={actorExists} passwordMatches={passwordMatches}");
            return Results.Content(Oauth_Authorize_Get.GetHtmlForAuthForm(requestUri!, clientId!, oauthRequest, true), "text/html");
        }
        else
        {
            Pds.Logger.LogInfo($"[OAUTH] Authentication succeeded. username={userName} actorExists={actorExists} passwordMatches={passwordMatches}");            
        }


        //
        // Validate redirect_uri against allowlist
        //
        string redirectUri = XrpcHelpers.GetRequestBodyArgumentValue(oauthRequest.Body, "redirect_uri");
        HashSet<string> allowedRedirectUris = Pds.PdsDb.GetConfigPropertyHashSet("OauthAllowedRedirectUris");
        if (!allowedRedirectUris.Contains(redirectUri))
        {
            Pds.Logger.LogWarning($"[OAUTH] [SECURITY] redirect_uri not in allowlist. redirect_uri={redirectUri}");
            return Results.Json(new { error = "invalid_redirect_uri" }, statusCode: 400);
        }


        //
        // Generate authorization code and update the oauth request
        //
        string authorizationCode = "authcode-" + Guid.NewGuid().ToString();
        oauthRequest.AuthorizationCode = authorizationCode;
        oauthRequest.AuthType = "Legacy";
        Pds.PdsDb.UpdateOauthRequest(oauthRequest);



        //
        // Build redirect URL and redirect
        //
        string state = XrpcHelpers.GetRequestBodyArgumentValue(oauthRequest.Body, "state");
        string issuer = $"https://{Pds.PdsDb.GetConfigProperty("PdsHostname")}";

        string redirectUrl = $"{redirectUri}?code={Uri.EscapeDataString(authorizationCode)}&state={Uri.EscapeDataString(state)}&iss={Uri.EscapeDataString(issuer)}";

        Pds.Logger.LogInfo($"[OAUTH] Redirecting to client. redirect_url={redirectUrl}");
        return Results.Redirect(redirectUrl);
    }
}