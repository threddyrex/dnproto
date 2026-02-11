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
        // Get all sessions (sorted by newest first)
        //
        var legacySessions = Pds.PdsDb.GetAllLegacySessions().OrderByDescending(s => s.CreatedDate).ToList();
        var oauthSessions = Pds.PdsDb.GetAllOauthSessions().OrderByDescending(s => s.CreatedDate).ToList();
        var adminSessions = Pds.PdsDb.GetAllAdminSessions().OrderByDescending(s => s.CreatedDate).ToList();

        //
        // Build session tables HTML
        //
        var enc = System.Text.Encodings.Web.HtmlEncoder.Default;

        string CalculateAge(string? createdDate)
        {
            if (string.IsNullOrEmpty(createdDate)) return "N/A";
            if (DateTimeOffset.TryParseExact(createdDate, "yyyy-MM-ddTHH:mm:ss.fffZ", 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, 
                out var created))
            {
                var elapsed = DateTimeOffset.UtcNow - created;
                return elapsed.TotalMinutes < 1 
                    ? $"{Math.Max(0, elapsed.TotalSeconds):F0}s" 
                    : $"{elapsed.TotalMinutes:F1}";
            }
            return "N/A";
        }

        string BuildLegacySessionsHtml()
        {
            if (legacySessions.Count == 0)
                return "<tr><td colspan=\"5\" style=\"text-align: center; color: #8899a6;\">No legacy sessions</td></tr>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var s in legacySessions)
            {
                sb.Append($@"<tr>
                    <td>{enc.Encode(s.CreatedDate)}</td>
                    <td style=""text-align: right;"">{enc.Encode(CalculateAge(s.CreatedDate))}</td>
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
                return "<tr><td colspan=\"6\" style=\"text-align: center; color: #8899a6;\">No OAuth sessions</td></tr>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var s in oauthSessions)
            {
                sb.Append($@"<tr>
                    <td>{enc.Encode(s.CreatedDate)}</td>
                    <td style=""text-align: right;"">{enc.Encode(CalculateAge(s.CreatedDate))}</td>
                    <td>{enc.Encode(s.IpAddress)}</td>
                    <td>{enc.Encode(s.ClientId)}</td>
                    <td>{enc.Encode(s.AuthType)}</td>
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
                return "<tr><td colspan=\"6\" style=\"text-align: center; color: #8899a6;\">No admin sessions</td></tr>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var s in adminSessions)
            {
                sb.Append($@"<tr>
                    <td>{enc.Encode(s.CreatedDate)}</td>
                    <td style=""text-align: right;"">{enc.Encode(CalculateAge(s.CreatedDate))}</td>
                    <td>{enc.Encode(s.IpAddress)}</td>
                    <td>{enc.Encode(s.UserAgent)}</td>
                    <td>{enc.Encode(s.AuthType)}</td>
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
            .sessions-table th.sortable {{ cursor: pointer; user-select: none; }}
            .sessions-table th.sortable:hover {{ background-color: #2a2d31; color: #e7e9ea; }}
            .sessions-table th.sortable::after {{ content: ' \2195'; opacity: 0.3; }}
            .sessions-table th.sortable.asc::after {{ content: ' \2191'; opacity: 1; }}
            .sessions-table th.sortable.desc::after {{ content: ' \2193'; opacity: 1; }}
            .sessions-table td {{ padding: 10px 16px; border-bottom: 1px solid #444; font-size: 14px; }}
            .sessions-table tr:last-child td {{ border-bottom: none; }}
            .sessions-table tr:hover {{ background-color: #3a3d41; }}
        </style>
        </head>
        <body>
        <div class=""container"">
        <div class=""navbar"">
            <a href=""/admin/"" class=""nav-btn"">Home</a>
            <a href=""/admin/config"" class=""nav-btn"">Config</a>
            <a href=""/admin/actions"" class=""nav-btn"">Actions</a>
            <a href=""/admin/passkeys"" class=""nav-btn"">Passkeys</a>
            <a href=""/admin/sessions"" class=""nav-btn active"">Sessions</a>
            <a href=""/admin/stats"" class=""nav-btn"">Statistics</a>
            <form method=""post"" action=""/admin/logout"" style=""margin: 0;"">
                <button type=""submit"" class=""logout-btn"">Logout</button>
            </form>
        </div>
        <h1>Sessions</h1>

        <h2>Legacy Sessions <span class=""session-count"">({legacySessions.Count})</span></h2>
        <table class=""sessions-table"" id=""legacySessionsTable"">
            <thead>
                <tr>
                    <th class=""sortable desc"" data-col=""0"" data-type=""string"">Created</th>
                    <th class=""sortable"" data-col=""1"" data-type=""number"" style=""text-align: right;"">Age (min)</th>
                    <th class=""sortable"" data-col=""2"" data-type=""string"">IP Address</th>
                    <th class=""sortable"" data-col=""3"" data-type=""string"">User Agent</th>
                    <th>Action</th>
                </tr>
            </thead>
            <tbody>
                {BuildLegacySessionsHtml()}
            </tbody>
        </table>

        <h2>OAuth Sessions <span class=""session-count"">({oauthSessions.Count})</span></h2>
        <table class=""sessions-table"" id=""oauthSessionsTable"">
            <thead>
                <tr>
                    <th class=""sortable desc"" data-col=""0"" data-type=""string"">Created</th>
                    <th class=""sortable"" data-col=""1"" data-type=""number"" style=""text-align: right;"">Age (min)</th>
                    <th class=""sortable"" data-col=""2"" data-type=""string"">IP Address</th>
                    <th class=""sortable"" data-col=""3"" data-type=""string"">Client ID</th>
                    <th class=""sortable"" data-col=""4"" data-type=""string"">Auth Type</th>
                    <th>Action</th>
                </tr>
            </thead>
            <tbody>
                {BuildOauthSessionsHtml()}
            </tbody>
        </table>

        <h2>Admin Sessions <span class=""session-count"">({adminSessions.Count})</span></h2>
        <table class=""sessions-table"" id=""adminSessionsTable"">
            <thead>
                <tr>
                    <th class=""sortable desc"" data-col=""0"" data-type=""string"">Created</th>
                    <th class=""sortable"" data-col=""1"" data-type=""number"" style=""text-align: right;"">Age (min)</th>
                    <th class=""sortable"" data-col=""2"" data-type=""string"">IP Address</th>
                    <th class=""sortable"" data-col=""3"" data-type=""string"">User Agent</th>
                    <th class=""sortable"" data-col=""4"" data-type=""string"">AuthType</th>
                    <th>Action</th>
                </tr>
            </thead>
            <tbody>
                {BuildAdminSessionsHtml()}
            </tbody>
        </table>
        </div>
        <script>
        // Table sorting for multiple tables
        (function() {{
            const tables = document.querySelectorAll('.sessions-table');
            
            tables.forEach(table => {{
                const headers = table.querySelectorAll('th.sortable');
                
                headers.forEach(header => {{
                    header.addEventListener('click', function() {{
                        const colIndex = parseInt(this.dataset.col);
                        const type = this.dataset.type;
                        const isDesc = this.classList.contains('desc');
                        
                        // Remove sort classes from all headers in this table
                        headers.forEach(h => h.classList.remove('asc', 'desc'));
                        
                        // Toggle sort direction (default to desc on first click)
                        const newDir = isDesc ? 'asc' : 'desc';
                        this.classList.add(newDir);
                        
                        sortTable(table, colIndex, type, newDir === 'asc');
                    }});
                }});
            }});
            
            function sortTable(table, colIndex, type, ascending) {{
                const tbody = table.querySelector('tbody');
                const rows = Array.from(tbody.querySelectorAll('tr'));
                
                rows.sort((a, b) => {{
                    const aCell = a.cells[colIndex];
                    const bCell = b.cells[colIndex];
                    
                    if (!aCell || !bCell) return 0;
                    
                    let aVal = aCell.textContent.trim();
                    let bVal = bCell.textContent.trim();
                    
                    if (type === 'number') {{
                        aVal = parseFloat(aVal) || 0;
                        bVal = parseFloat(bVal) || 0;
                        return ascending ? aVal - bVal : bVal - aVal;
                    }} else {{
                        return ascending 
                            ? aVal.localeCompare(bVal)
                            : bVal.localeCompare(aVal);
                    }}
                }});
                
                rows.forEach(row => tbody.appendChild(row));
            }}
        }})();
        </script>
        </body>
        </html>
        ";
        return Results.Content(html, "text/html", statusCode: 200);
    }
}
