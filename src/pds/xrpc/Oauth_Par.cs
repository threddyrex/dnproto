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
    public IResult GetResponse()
    {
        //
        // Get dpop header and body
        //
        string? dpopHeader = HttpContext.Request.Headers["DPoP"];
        string? body = HttpContext.Request.Body != null ? new StreamReader(HttpContext.Request.Body).ReadToEnd() : null;

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
            $"https://{Pds.Config.PdsHostname}/oauth/par");

        if (!dpopResult.IsValid)
        {
            Pds.Logger.LogWarning($"[OAUTH] dpop validation failed. error={dpopResult.Error}");
            return Results.Json(new{}, statusCode: 401);
        }



        //
        // Insert into db
        //
        Pds.PdsDb.InsertOauthRequest(oauthRequest);



        //
        // Return success
        //
        Pds.Logger.LogInfo($"[OAUTH] par success. request_uri={oauthRequest.RequestUri} expires_in={expiresSeconds}");
        return Results.Json(new JsonObject()
        {
            ["request_uri"] = oauthRequest.RequestUri,
            ["expires_in"] = expiresSeconds
        },
        statusCode: 201);

    }
}