using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// Sessions page for admin interface
/// </summary>
public class Admin_Sessions : BaseAdmin
{
    public IResult GetResponse()
    {
        IncrementStatistics();
        
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
        // Build session tables HTML
        //
        var enc = System.Text.Encodings.Web.HtmlEncoder.Default;

        string BuildLegacySessionsHtml()
        {
            if (legacySessions.Count == 0)
                return "<tr><td colspan=\"4\" style=\"text-align: center; color: #8899a6;\">No legacy sessions</td></tr>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var s in legacySessions)
            {
                sb.Append($@"<tr>
                    <td>{enc.Encode(s.CreatedDate)}</td>
                    <td>{enc.Encode(s.IpAddress)}</td>
                    <td>{enc.Encode(s.UserAgent)}</td>
                    <td>
                        <form method=""post"" action=""/admin/deletelegacysession"" style=""display:inline;"">
                            <input type=""hidden"" name=""refreshJwt"" value=""{enc.Encode(s.RefreshJwt)}"" />
                            <button type=""submit"" class=""delete-btn"">Delete</button>
                        </form>
                    </td>
                </tr>");
            }
            return sb.ToString();
        }

        string BuildOauthSessionsHtml()
        {
            if (oauthSessions.Count == 0)
                return "<tr><td colspan=\"5\" style=\"text-align: center; color: #8899a6;\">No OAuth sessions</td></tr>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var s in oauthSessions)
            {
                sb.Append($@"<tr>
                    <td>{enc.Encode(s.CreatedDate)}</td>
                    <td>{enc.Encode(s.IpAddress)}</td>
                    <td>{enc.Encode(s.SessionId)}</td>
                    <td>{enc.Encode(s.ClientId)}</td>
                    <td>
                        <form method=""post"" action=""/admin/deleteoauthsession"" style=""display:inline;"">
                            <input type=""hidden"" name=""sessionId"" value=""{enc.Encode(s.SessionId)}"" />
                            <button type=""submit"" class=""delete-btn"">Delete</button>
                        </form>
                    </td>
                </tr>");
            }

            return sb.ToString();
        }

        string BuildAdminSessionsHtml()
        {
            if (adminSessions.Count == 0)
                return "<tr><td colspan=\"5\" style=\"text-align: center; color: #8899a6;\">No admin sessions</td></tr>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var s in adminSessions)
            {
                sb.Append($@"<tr>
                    <td>{enc.Encode(s.CreatedDate)}</td>
                    <td>{enc.Encode(s.IpAddress)}</td>
                    <td>{enc.Encode(s.UserAgent)}</td>
                    <td>{enc.Encode(s.SessionId)}</td>
                    <td>
                        <form method=""post"" action=""/admin/deleteadminsession"" style=""display:inline;"">
                            <input type=""hidden"" name=""sessionId"" value=""{enc.Encode(s.SessionId)}"" />
                            <button type=""submit"" class=""delete-btn"">Delete</button>
                        </form>
                    </td>
                </tr>");
            }
            return sb.ToString();
        }


        //
        // return sessions page
        //
        string html = $@"
        <html>
        <head>
        <title>Admin - Sessions</title>
        <style>
            body {{ background-color: #16181c; color: #e7e9ea; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; padding: 40px 20px; }}
            .container {{ max-width: 800px; margin: 0 0 0 40px; }}
            h1 {{ color: #8899a6; margin-bottom: 24px; }}
            h2 {{ color: #8899a6; margin-top: 32px; margin-bottom: 16px; font-size: 18px; }}
            .navbar {{ display: flex; justify-content: flex-end; align-items: center; gap: 12px; margin-bottom: 24px; padding-bottom: 16px; border-bottom: 1px solid #2f3336; }}
            .nav-btn {{ background-color: #4caf50; color: white; border: none; padding: 10px 20px; border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 500; text-decoration: none; }}
            .nav-btn:hover {{ background-color: #388e3c; }}
            .nav-btn.active {{ background-color: #388e3c; }}
            .logout-btn {{ background-color: #f44336; color: white; border: none; padding: 10px 20px; border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 500; }}
            .logout-btn:hover {{ background-color: #d32f2f; }}
            .delete-btn {{ background-color: #f44336; color: white; border: none; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; font-weight: 500; }}
            .delete-btn:hover {{ background-color: #d32f2f; }}
            .session-count {{ color: #8899a6; font-size: 14px; margin-left: 8px; }}
            .sessions-table {{ width: 100%; border-collapse: collapse; background-color: #2f3336; border-radius: 8px; overflow: hidden; margin-bottom: 24px; }}
            .sessions-table th {{ background-color: #1d1f23; color: #8899a6; text-align: left; padding: 12px 16px; font-size: 14px; font-weight: 500; }}
            .sessions-table td {{ padding: 10px 16px; border-bottom: 1px solid #444; font-size: 14px; }}
            .sessions-table tr:last-child td {{ border-bottom: none; }}
            .sessions-table tr:hover {{ background-color: #3a3d41; }}
        </style>
        </head>
        <body>
        <div class=""container"">
        <div class=""navbar"">
            <a href=""/admin/"" class=""nav-btn"">Home</a>
            <a href=""/admin/sessions"" class=""nav-btn active"">Sessions</a>
            <a href=""/admin/stats"" class=""nav-btn"">Statistics</a>
            <form method=""post"" action=""/admin/logout"" style=""margin: 0;"">
                <button type=""submit"" class=""logout-btn"">Logout</button>
            </form>
        </div>
        <h1>Sessions</h1>

        <h2>Legacy Sessions <span class=""session-count"">({legacySessions.Count})</span></h2>
        <table class=""sessions-table"">
            <thead>
                <tr>
                    <th>Created</th>
                    <th>IP Address</th>
                    <th>User Agent</th>
                    <th>Action</th>
                </tr>
            </thead>
            <tbody>
                {BuildLegacySessionsHtml()}
            </tbody>
        </table>

        <h2>OAuth Sessions <span class=""session-count"">({oauthSessions.Count})</span></h2>
        <table class=""sessions-table"">
            <thead>
                <tr>
                    <th>Created</th>
                    <th>IP Address</th>
                    <th>Session ID</th>
                    <th>Client ID</th>
                    <th>Action</th>
                </tr>
            </thead>
            <tbody>
                {BuildOauthSessionsHtml()}
            </tbody>
        </table>

        <h2>Admin Sessions <span class=""session-count"">({adminSessions.Count})</span></h2>
        <table class=""sessions-table"">
            <thead>
                <tr>
                    <th>Created</th>
                    <th>IP Address</th>
                    <th>User Agent</th>
                    <th>Session ID</th>
                    <th>Action</th>
                </tr>
            </thead>
            <tbody>
                {BuildAdminSessionsHtml()}
            </tbody>
        </table>
        </div>
        </body>
        </html>
        ";
        return Results.Content(html, "text/html", statusCode: 200);
    }
}
