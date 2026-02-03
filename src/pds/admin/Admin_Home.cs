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
        // Get all sessions
        //
        var legacySessions = Pds.PdsDb.GetAllLegacySessions();
        var oauthSessions = Pds.PdsDb.GetAllOauthSessions();
        var adminSessions = Pds.PdsDb.GetAllAdminSessions();

        //
        // Build session lists HTML
        //
        string BuildLegacySessionsHtml()
        {
            if (legacySessions.Count == 0)
                return "<div class=\"session-item\">No legacy sessions</div>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var s in legacySessions)
            {
                sb.Append($@"<div class=""session-item""><span class=""session-label"">Created:</span> {System.Net.WebUtility.HtmlEncode(s.CreatedDate)} <span class=""session-label"">IP:</span> {System.Net.WebUtility.HtmlEncode(s.IpAddress)} <span class=""session-label"">User-Agent:</span> {System.Net.WebUtility.HtmlEncode(s.UserAgent)}</div>");
            }
            return sb.ToString();
        }

        string BuildOauthSessionsHtml()
        {
            if (oauthSessions.Count == 0)
                return "<div class=\"session-item\">No OAuth sessions</div>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var s in oauthSessions)
            {
                sb.Append($@"<div class=""session-item"">
                    <span class=""session-label"">Created:</span> {System.Net.WebUtility.HtmlEncode(s.CreatedDate)} 
                    <span class=""session-label"">IP:</span> {System.Net.WebUtility.HtmlEncode(s.IpAddress)} 
                    <span class=""session-label"">Session:</span> {System.Net.WebUtility.HtmlEncode(s.SessionId)} 
                    <span class=""session-label"">Client:</span> {System.Net.WebUtility.HtmlEncode(s.ClientId)}
                    <form method=""post"" action=""/admin/deleteoauthsession"" style=""display:inline; margin-left: 12px;"">
                        <input type=""hidden"" name=""sessionId"" value=""{System.Net.WebUtility.HtmlEncode(s.SessionId)}"" />
                        <button type=""submit"" class=""delete-btn"">Delete</button>
                    </form>
                </div>");
            }

            return sb.ToString();
        }

        string BuildAdminSessionsHtml()
        {
            if (adminSessions.Count == 0)
                return "<div class=\"session-item\">No admin sessions</div>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var s in adminSessions)
            {
                sb.Append($@"<div class=""session-item""><span class=""session-label"">Created:</span> {System.Net.WebUtility.HtmlEncode(s.CreatedDate)} <span class=""session-label"">IP:</span> {System.Net.WebUtility.HtmlEncode(s.IpAddress)} <span class=""session-label"">Session:</span> {System.Net.WebUtility.HtmlEncode(s.SessionId)}</div>");
            }
            return sb.ToString();
        }


        //
        // return account info
        //
        string html = $@"
        <html>
        <head>
        <title>Admin - Home</title>
        <style>
            body {{ background-color: #16181c; color: #e7e9ea; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; padding: 40px 20px; }}
            .container {{ max-width: 800px; margin: 0 0 0 40px; }}
            h1 {{ color: #8899a6; margin-bottom: 24px; }}
            h2 {{ color: #8899a6; margin-top: 32px; margin-bottom: 16px; font-size: 18px; }}
            .info-card {{ background-color: #2f3336; border-radius: 8px; padding: 20px; margin-bottom: 16px; }}
            .label {{ color: #8899a6; font-size: 14px; margin-bottom: 4px; }}
            .value {{ color: #1d9bf0; font-size: 16px; word-break: break-all; }}
            .session-list {{ background-color: #2f3336; border-radius: 8px; padding: 16px; margin-bottom: 16px; }}
            .session-item {{ padding: 8px 0; border-bottom: 1px solid #444; font-size: 14px; }}
            .session-item:last-child {{ border-bottom: none; }}
            .session-label {{ color: #8899a6; margin-right: 4px; }}
            .session-count {{ color: #8899a6; font-size: 14px; margin-left: 8px; }}
            .logout-btn {{ background-color: #f44336; color: white; border: none; padding: 10px 20px; border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 500; }}
            .logout-btn:hover {{ background-color: #d32f2f; }}
            .delete-btn {{ background-color: #f44336; color: white; border: none; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; font-weight: 500; }}
            .delete-btn:hover {{ background-color: #d32f2f; }}
            .header {{ display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }}
            .header h1 {{ margin-bottom: 0; }}
        </style>
        </head>
        <body>
        <div class=""container"">
        <div class=""header"">
            <h1>Admin Dashboard</h1>
            <form method=""post"" action=""/admin/logout"">
                <button type=""submit"" class=""logout-btn"">Logout</button>
            </form>
        </div>
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

        <h2>Legacy Sessions <span class=""session-count"">({legacySessions.Count})</span></h2>
        <div class=""session-list"">
            {BuildLegacySessionsHtml()}
        </div>

        <h2>OAuth Sessions <span class=""session-count"">({oauthSessions.Count})</span></h2>
        <div class=""session-list"">
            {BuildOauthSessionsHtml()}
        </div>

        <h2>Admin Sessions <span class=""session-count"">({adminSessions.Count})</span></h2>
        <div class=""session-list"">
            {BuildAdminSessionsHtml()}
        </div>
        </div>
        </body>
        </html>
        ";
        return Results.Content(html, "text/html", statusCode: 200);
    }





}