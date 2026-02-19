using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.log;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class Oauth_Par : BaseXrpcCommand
{
    public async Task<IResult> GetResponse()
    {
        if(!IsOauthEnabled())
        {
            return Results.Json(new{}, statusCode: 403);
        }

        IncrementStatistics();

        
        //
        // Get dpop header and body
        //
        string? dpopHeader = HttpContext.Request.Headers["DPoP"];
        string? body = HttpContext.Request.Body != null ? await new StreamReader(HttpContext.Request.Body).ReadToEndAsync() : null;


        if(string.IsNullOrEmpty(dpopHeader) || string.IsNullOrEmpty(body))
        {
            Pds.Logger.LogWarning($"[OAUTH] dpop or body is null. dpopHeader={dpopHeader} body={body}");
            return Results.Json(new{}, statusCode: 401);
        }


        //
        // Create new OauthRequest for db
        //
        int expiresSeconds = 300;
        var oauthRequest = new OauthRequest()
        {
            Dpop = dpopHeader,
            Body = body,
            RequestUri = "urn:ietf:params:oauth:request_uri:" + Guid.NewGuid().ToString(),
            ExpiresDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow.AddSeconds(expiresSeconds))
        };



        //
        // Validate dpop
        //
        var dpopResult = JwtSecret.ValidateDpop(
            dpopHeader, 
            "POST", 
            $"https://{Pds.PdsDb.GetConfigProperty("PdsHostname")}/oauth/par");

        if (!dpopResult.IsValid)
        {
            Pds.Logger.LogWarning($"[AUTH] [OAUTH] dpop validation failed. error={dpopResult.Error}");
            Pds.Logger.LogWarning($"[AUTH] [OAUTH] debug: {dpopResult.DebugInfo}");
            return Results.Json(new{}, statusCode: 401);
        }


        //
        // Validate redirect_uri against allowlist
        //
        string? redirectUri = XrpcHelpers.GetRequestBodyArgumentValue(body, "redirect_uri");
        HashSet<string> allowedRedirectUris = Pds.PdsDb.GetConfigPropertyHashSet("OauthAllowedRedirectUris");
        if (!allowedRedirectUris.Contains(redirectUri ?? ""))
        {
            Pds.Logger.LogWarning($"[OAUTH] [SECURITY] PAR redirect_uri not in allowlist. redirect_uri={redirectUri}");
            return Results.Json(new { error = "invalid_redirect_uri" }, statusCode: 400);
        }


        //
        // Insert into db
        //
        Pds.PdsDb.InsertOauthRequest(oauthRequest);



        //
        // Return success
        //
        Pds.Logger.LogInfo($"[AUTH] [OAUTH] par success. request_uri={oauthRequest.RequestUri} expires_in={expiresSeconds}");
        return Results.Json(new JsonObject()
        {
            ["request_uri"] = oauthRequest.RequestUri,
            ["expires_in"] = expiresSeconds
        },
        statusCode: 201);

    }
}