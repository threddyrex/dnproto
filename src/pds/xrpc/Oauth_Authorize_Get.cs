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
        if(!IsOauthEnabled())
        {
            return Results.Json(new{}, statusCode: 403);
        }

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


        return Results.Content(GetHtmlForAuthForm(requestUri, clientId, oauthRequest), "text/html");

    }



    public static string GetHtmlForAuthForm(string requestUri, string clientId, OauthRequest oauthRequest, bool failed = false)
    {
        //
        // Get values from the oauth request and HTML-encode to prevent XSS
        //
        string safeRequestUri = System.Net.WebUtility.HtmlEncode(requestUri);
        string safeClientId = System.Net.WebUtility.HtmlEncode(clientId);
        string safeScope = System.Net.WebUtility.HtmlEncode(GetRequestBodyArgumentValue(oauthRequest.Body,"scope"));

        //
        // Render HTML to capture username and password.
        //
        string html = $@"
        <html>
        <head>
        <title>Authorize {safeClientId}</title>
        <style>
            body {{ background-color: #16181c; color: #e7e9ea; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; padding: 40px 20px; }}
            .container {{ max-width: 500px; margin: 0 0 0 40px; }}
            h1 {{ color: #8899a6; margin-bottom: 24px; }}
            p {{ margin-bottom: 16px; line-height: 1.5; }}
            code {{ background-color: #2f3336; padding: 2px 6px; border-radius: 4px; }}
            label {{ display: block; margin-bottom: 6px; color: #8899a6; }}
            input[type=""text""], input[type=""password""] {{ width: 100%; padding: 12px; margin-bottom: 16px; background-color: #2f3336; border: 1px solid #3d4144; border-radius: 6px; color: #e7e9ea; font-size: 16px; box-sizing: border-box; }}
            input:focus {{ outline: none; border-color: #1d9bf0; }}
            button {{ background-color: #1d9bf0; color: white; border: none; padding: 12px 24px; border-radius: 6px; font-size: 16px; font-weight: bold; cursor: pointer; }}
            button:hover {{ background-color: #1a8cd8; }}
        </style>
        </head>
        <body>
        <div class=""container"">
        <h1>Authorize {safeClientId}</h1>
        {(failed ? "<p style=\"color: red;\">Authentication failed. Please try again.</p>" : "")}
        <p><strong>{safeClientId}</strong> is requesting access to your account.</p>
        <p>Requested permissions: <code>{safeScope}</code></p>
        <form method=""post"" action=""/oauth/authorize"">
            <input type=""hidden"" name=""request_uri"" value=""{safeRequestUri}"" />
            <input type=""hidden"" name=""client_id"" value=""{safeClientId}"" />
            <label for=""username"">Username</label>
            <input type=""text"" id=""username"" name=""username"" />
            <label for=""password"">Password</label>
            <input type=""password"" id=""password"" name=""password"" />
            <button type=""submit"">Authorize</button>
        </form>
        </div>
        </body>
        </html>
        ";

        return html;
    }
}