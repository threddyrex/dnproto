using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// </summary>
public class Admin_Home : BaseAdmin
{
    public IResult GetResponse()
    {
        if(AdminInterfaceIsEnabled() == false)
        {
            return Results.StatusCode(404);
        }


        //
        // Require auth
        //
        if(AdminIsAuthenticated() == false)
        {
            // redirect to /admin/login
            HttpContext.Response.Redirect("/admin/login");
            return Results.Empty;
        }


        AdminSession? adminSession = GetValidAdminSession();


        //
        // return account info
        //
        string html = $@"
        <html>
        <head>
        <title>Admin - Home</title>
        <style>
            body {{ background-color: #16181c; color: #e7e9ea; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; padding: 40px 20px; }}
            .container {{ max-width: 600px; margin: 0 0 0 40px; }}
            h1 {{ color: #8899a6; margin-bottom: 24px; }}
            .info-card {{ background-color: #2f3336; border-radius: 8px; padding: 20px; margin-bottom: 16px; }}
            .label {{ color: #8899a6; font-size: 14px; margin-bottom: 4px; }}
            .value {{ color: #1d9bf0; font-size: 16px; word-break: break-all; }}
        </style>
        </head>
        <body>
        <div class=""container"">
        <h1>Admin Dashboard</h1>
        <div class=""info-card"">
            <div class=""label"">admin session id:</div>
            <div class=""value"">{System.Net.WebUtility.HtmlEncode(adminSession?.SessionId ?? "N/A")}</div>
        </div>
        <div class=""info-card"">
            <div class=""label"">admin session created at:</div>
            <div class=""value"">{System.Net.WebUtility.HtmlEncode(adminSession?.CreatedDate ?? "N/A")}</div>
        </div>
        <div class=""info-card"">
            <div class=""label"">pds did</div>
            <div class=""value"">{System.Net.WebUtility.HtmlEncode(Pds.Config.PdsDid)}</div>
        </div>
        <div class=""info-card"">
            <div class=""label"">user did</div>
            <div class=""value"">{System.Net.WebUtility.HtmlEncode(Pds.Config.UserDid)}</div>
        </div>
        <div class=""info-card"">
            <div class=""label"">user handle</div>
            <div class=""value"">{System.Net.WebUtility.HtmlEncode(Pds.Config.UserHandle)}</div>
        </div>
        <div class=""info-card"">
            <div class=""label"">user email</div>
            <div class=""value"">{System.Net.WebUtility.HtmlEncode(Pds.Config.UserEmail)}</div>
        </div>
        </div>
        </body>
        </html>
        ";
        return Results.Content(html, "text/html", statusCode: 200);
    }





}