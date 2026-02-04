using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class Oauth_Revoke : BaseXrpcCommand
{
    public async Task<IResult> GetResponse()
    {
        if(!IsOauthEnabled())
        {
            return Results.Json(new{}, statusCode: 403);
        }

        //
        // Get the token param
        //
        string? body = HttpContext.Request.Body != null ? await new StreamReader(HttpContext.Request.Body).ReadToEndAsync() : null;
        if(body is null || string.IsNullOrEmpty(body))
        {
            return Results.Json(new{}, statusCode: 400);
        }
        string? token = XrpcHelpers.GetRequestBodyArgumentValue(body, "token");
        if(string.IsNullOrEmpty(token))
        {
            return Results.Json(new{}, statusCode: 400);
        }

        //
        // Delete the OAuth session associated with the refresh token
        //
        Pds.PdsDb.DeleteOauthSessionByRefreshToken(token);

        return Results.Json(new
        {
            keys = new JsonArray(){},
        },
        statusCode: 200);
    }
}