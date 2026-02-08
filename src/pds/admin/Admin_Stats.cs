using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// Statistics page for admin interface
/// </summary>
public class Admin_Stats : BaseAdmin
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
        // Get all statistics
        //
        var statistics = Pds.PdsDb.GetAllStatistics().OrderByDescending(s => s.LastUpdatedDate).ToList();

        string BuildStatisticsHtml()
        {
            var enc = System.Text.Encodings.Web.HtmlEncoder.Default;

            if (statistics.Count == 0)
                return "<tr><td colspan=\"7\" style=\"text-align: center; color: #8899a6;\">No statistics</td></tr>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var s in statistics)
            {
                string minutesAgo = "N/A";
                if (DateTimeOffset.TryParseExact(s.LastUpdatedDate, "yyyy-MM-ddTHH:mm:ss.fffZ", 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, 
                    out var lastUpdated))
                {
                    var elapsed = DateTimeOffset.UtcNow - lastUpdated;
                    var totalMinutes = Math.Max(0, elapsed.TotalMinutes);
                    minutesAgo = $"{totalMinutes:F1}m";
                }
                sb.Append($@"<tr>
                    <td>{enc.Encode(s.Name)}</td>
                    <td>{enc.Encode(s.IpAddress)}</td>
                    <td>{enc.Encode(s.UserAgent)}</td>
                    <td style=""text-align: right;"">{enc.Encode(s.Value.ToString())}</td>
                    <td>{enc.Encode(s.LastUpdatedDate)}</td>
                    <td style=""text-align: right;"">{enc.Encode(minutesAgo)}</td>
                    <td>
                        <form method=""post"" action=""/admin/deletestatistic"" style=""display:inline;"">
                            <input type=""hidden"" name=""name"" value=""{enc.Encode(s.Name)}"" />
                            <input type=""hidden"" name=""ipAddress"" value=""{enc.Encode(s.IpAddress)}"" />
                            <input type=""hidden"" name=""userAgent"" value=""{enc.Encode(s.UserAgent)}"" />
                            <button type=""submit"" class=""delete-btn"">Delete</button>
                        </form>
                    </td>
                </tr>");
            }
            return sb.ToString();
        }


        //
        // return statistics page
        //
        string html = $@"
        <html>
        <head>
        <title>Admin - Statistics</title>
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
            .delete-all-btn {{ background-color: #f44336; color: white; border: none; padding: 8px 16px; border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 500; }}
            .delete-all-btn:hover {{ background-color: #d32f2f; }}
            .section-header {{ display: flex; justify-content: space-between; align-items: center; }}
            .session-count {{ color: #8899a6; font-size: 14px; margin-left: 8px; }}
            .stats-table {{ width: 100%; border-collapse: collapse; background-color: #2f3336; border-radius: 8px; overflow: hidden; }}
            .stats-table th {{ background-color: #1d1f23; color: #8899a6; text-align: left; padding: 12px 16px; font-size: 14px; font-weight: 500; }}
            .stats-table th.sortable {{ cursor: pointer; user-select: none; }}
            .stats-table th.sortable:hover {{ background-color: #2a2d31; color: #e7e9ea; }}
            .stats-table th.sortable::after {{ content: ' \2195'; opacity: 0.3; }}
            .stats-table th.sortable.asc::after {{ content: ' \2191'; opacity: 1; }}
            .stats-table th.sortable.desc::after {{ content: ' \2193'; opacity: 1; }}
            .stats-table td {{ padding: 10px 16px; border-bottom: 1px solid #444; font-size: 14px; }}
            .stats-table tr:last-child td {{ border-bottom: none; }}
            .stats-table tr:hover {{ background-color: #3a3d41; }}
        </style>
        </head>
        <body>
        <div class=""container"">
        <div class=""navbar"">
            <a href=""/admin/"" class=""nav-btn"">Home</a>
            <a href=""/admin/passkeys"" class=""nav-btn"">Passkeys</a>
            <a href=""/admin/sessions"" class=""nav-btn"">Sessions</a>
            <a href=""/admin/stats"" class=""nav-btn active"">Statistics</a>
            <form method=""post"" action=""/admin/logout"" style=""margin: 0;"">
                <button type=""submit"" class=""logout-btn"">Logout</button>
            </form>
        </div>
        <h1>Statistics</h1>

        <div class=""section-header"">
            <h2>Statistics <span class=""session-count"">({statistics.Count})</span></h2>
            <div style=""display: flex; gap: 8px;"">
                <form method=""post"" action=""/admin/deleteallstatistics"" style=""display:inline;"" onsubmit=""return confirm('Are you sure you want to delete all statistics?');"">
                    <button type=""submit"" class=""delete-all-btn"">Delete All</button>
                </form>
                <form method=""post"" action=""/admin/deleteoldstatistics"" style=""display:inline;"" onsubmit=""return confirm('Are you sure you want to delete statistics older than 24 hours?');"">
                    <button type=""submit"" class=""delete-all-btn"">Delete Old (&lt;24hr)</button>
                </form>
            </div>
        </div>
        <div style=""margin-bottom: 16px; display: flex; gap: 12px;"">
            <input type=""text"" id=""showFilterInput"" placeholder=""Show..."" style=""flex: 1; padding: 10px 14px; font-size: 14px; background-color: #2f3336; color: #e7e9ea; border: 1px solid #444; border-radius: 6px; outline: none;"" onfocus=""this.style.borderColor='#4caf50'"" onblur=""this.style.borderColor='#444'"" />
            <input type=""text"" id=""hideFilterInput"" placeholder=""Hide..."" style=""flex: 1; padding: 10px 14px; font-size: 14px; background-color: #2f3336; color: #e7e9ea; border: 1px solid #444; border-radius: 6px; outline: none;"" onfocus=""this.style.borderColor='#f44336'"" onblur=""this.style.borderColor='#444'"" />
        </div>
        <table class=""stats-table"" id=""statsTable"">
            <thead>
                <tr>
                    <th class=""sortable"" data-col=""0"" data-type=""string"">Name</th>
                    <th class=""sortable"" data-col=""1"" data-type=""string"">IP Address</th>
                    <th class=""sortable"" data-col=""2"" data-type=""string"">User Agent</th>
                    <th class=""sortable"" data-col=""3"" data-type=""number"" style=""text-align: right;"">Value</th>
                    <th class=""sortable desc"" data-col=""4"" data-type=""string"">Last Updated</th>
                    <th class=""sortable"" data-col=""5"" data-type=""number"" style=""text-align: right;"">Minutes Ago</th>
                    <th>Action</th>
                </tr>
            </thead>
            <tbody>
                {BuildStatisticsHtml()}
            </tbody>
        </table>
        </div>
        <script>
        // Table sorting
        (function() {{
            const table = document.getElementById('statsTable');
            if (!table) return;
            
            const headers = table.querySelectorAll('th.sortable');
            
            headers.forEach(header => {{
                header.addEventListener('click', function() {{
                    const colIndex = parseInt(this.dataset.col);
                    const type = this.dataset.type;
                    const isDesc = this.classList.contains('desc');
                    
                    // Remove sort classes from all headers
                    headers.forEach(h => h.classList.remove('asc', 'desc'));
                    
                    // Toggle sort direction (default to desc on first click)
                    const newDir = isDesc ? 'asc' : 'desc';
                    this.classList.add(newDir);
                    
                    sortTable(colIndex, type, newDir === 'asc');
                }});
            }});
            
            function sortTable(colIndex, type, ascending) {{
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

        // Table filtering
        (function() {{
            const showFilterInput = document.getElementById('showFilterInput');
            const hideFilterInput = document.getElementById('hideFilterInput');
            const table = document.getElementById('statsTable');
            if (!showFilterInput || !hideFilterInput || !table) return;
            
            function applyFilters() {{
                const showText = showFilterInput.value.toLowerCase();
                const hideText = hideFilterInput.value.toLowerCase();
                const tbody = table.querySelector('tbody');
                const rows = tbody.querySelectorAll('tr');
                
                rows.forEach(row => {{
                    const cells = row.querySelectorAll('td');
                    let rowText = '';
                    cells.forEach(cell => {{
                        rowText += cell.textContent.toLowerCase() + ' ';
                    }});
                    
                    // Hide filter takes precedence
                    if (hideText && rowText.includes(hideText)) {{
                        row.style.display = 'none';
                        return;
                    }}
                    
                    // Show filter: if empty, show all; otherwise must match
                    if (showText && !rowText.includes(showText)) {{
                        row.style.display = 'none';
                        return;
                    }}
                    
                    row.style.display = '';
                }});
            }}
            
            showFilterInput.addEventListener('input', applyFilters);
            hideFilterInput.addEventListener('input', applyFilters);
        }})();
        </script>
        </body>
        </html>
        ";
        return Results.Content(html, "text/html", statusCode: 200);
    }
}
