using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.log;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class Oauth_Authorize_Get : BaseXrpcCommand
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
        // Get values from the oauth request and HTML-encode to prevent XSS
        //
        string safeRequestUri = System.Net.WebUtility.HtmlEncode(requestUri);
        string safeClientId = System.Net.WebUtility.HtmlEncode(clientId);
        string safeScope = System.Net.WebUtility.HtmlEncode(oauthRequest.GetRequestBodyArgumentValue("scope"));

        //
        // Render HTML to capture username and password.
        //
        string html = $@"
        <html>
        <head>
        <title>Authorize {safeClientId}</title>
        <style>body {{ background-color: #1a237e; color: white; }}</style>
        </head>
        <body>
        <h1>Authorize {safeClientId}</h1>
        <p><strong>{safeClientId}</strong> is requesting access to your account.</p>
        <p>Requested permissions: <code>{safeScope}</code></p>
        <form method=""post"" action=""/oauth/authorize"">
            <input type=""hidden"" name=""request_uri"" value=""{safeRequestUri}"" />
            <input type=""hidden"" name=""client_id"" value=""{safeClientId}"" />
            <label for=""username"">Username:</label> <br />
            <input type=""text"" id=""username"" name=""username"" /> <br />
            <label for=""password"">Password:</label> <br />
            <input type=""password"" id=""password"" name=""password"" /> <br />
            <button type=""submit"">Authorize</button>
        </form>
        </body>
        </html>
        ";

        return Results.Content(html, "text/html");

    }
}