using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.log;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class Oauth_Authorize : BaseXrpcCommand
{
    public async Task<IResult> GetResponse()
    {
        //
        // Get client_id and request_uri from query parameters
        //
        string? clientId = GetQueryParameter("client_id");
        string? requestUri = GetQueryParameter("request_uri");

        if(string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(requestUri))
        {
            Pds.Logger.LogWarning($"[OAUTH] Missing parameters. client_id={clientId} request_uri={requestUri}");
            return Results.Json(new{}, statusCode: 401);            
        }


        //
        // Load 
        //
        if(!Pds.PdsDb.OauthRequestExists(requestUri))
        {
            Pds.Logger.LogWarning($"[OAUTH] Oauth request does not exist or has expired. request_uri={requestUri}");
            return Results.Json(new{}, statusCode: 401);
        }

        OauthRequest oauthRequest = Pds.PdsDb.GetOauthRequest(requestUri);


        //
        // Render HTML to capture username and password.
        //
        string html = $@"
        <html>
        <head>
        <title>Authorize</title>
        </head>
        <body>
        <form method='post' action='/oauth/authorize'>
            <input type='hidden' name='request_uri' value='{requestUri}' />
            <input type='hidden' name='client_id' value='{clientId}' />
            <label for='username'>Username:</label>
            <input type='text' id='username' name='username' />
            <label for='password'>Password:</label>
            <input type='password' id='password' name='password' />
            <button type='submit'>Authorize</button>
        </form>
        </body>
        </html>
        ";

        return Results.Content(html, "text/html");

    }
}