using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// Passkeys page for admin interface
/// </summary>
public class Admin_Passkeys : BaseAdmin
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
        // Get passkeys and challenges
        //
        var passkeys = Pds.PdsDb.GetAllPasskeys();
        var passkeyChallenges = Pds.PdsDb.GetAllPasskeyChallenges();

        //
        // Build passkey lists HTML
        //
        var enc = System.Text.Encodings.Web.HtmlEncoder.Default;

        string BuildPasskeysHtml()
        {
            if (passkeys.Count == 0)
                return "<tr><td colspan=\"3\" style=\"text-align: center; color: #8899a6;\">No passkeys</td></tr>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var p in passkeys)
            {
                sb.Append($@"<tr>
                    <td>{enc.Encode(p.Name)}</td>
                    <td>{enc.Encode(p.CreatedDate)}</td>
                    <td>
                        <form method=""post"" action=""/admin/deletepasskey"" style=""display:inline;"">
                            <input type=""hidden"" name=""name"" value=""{enc.Encode(p.Name)}"" />
                            <button type=""submit"" class=""delete-btn"">Delete</button>
                        </form>
                    </td>
                </tr>");
            }
            return sb.ToString();
        }

        string BuildPasskeyChallengesHtml()
        {
            if (passkeyChallenges.Count == 0)
                return "<tr><td colspan=\"3\" style=\"text-align: center; color: #8899a6;\">No passkey challenges</td></tr>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var c in passkeyChallenges)
            {
                sb.Append($@"<tr>
                    <td>{enc.Encode(c.Challenge)}</td>
                    <td>{enc.Encode(c.CreatedDate)}</td>
                    <td>
                        <form method=""post"" action=""/admin/deletepasskeychallenge"" style=""display:inline;"">
                            <input type=""hidden"" name=""challenge"" value=""{enc.Encode(c.Challenge)}"" />
                            <button type=""submit"" class=""delete-btn"">Delete</button>
                        </form>
                    </td>
                </tr>");
            }
            return sb.ToString();
        }


        //
        // return passkeys page
        //
        string html = $@"
        <html>
        <head>
        <title>Admin - Passkeys</title>
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
            .add-btn {{ background-color: #4caf50; color: white; border: none; padding: 8px 16px; border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 500; }}
            .add-btn:hover {{ background-color: #388e3c; }}
            .section-header {{ display: flex; justify-content: space-between; align-items: center; }}
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
            <a href=""/admin/config"" class=""nav-btn"">Config</a>
            <a href=""/admin/passkeys"" class=""nav-btn active"">Passkeys</a>
            <a href=""/admin/sessions"" class=""nav-btn"">Sessions</a>
            <a href=""/admin/stats"" class=""nav-btn"">Statistics</a>
            <form method=""post"" action=""/admin/logout"" style=""margin: 0;"">
                <button type=""submit"" class=""logout-btn"">Logout</button>
            </form>
        </div>
        <h1>Passkeys</h1>

        <div class=""section-header"">
            <h2>Passkeys <span class=""session-count"">({passkeys.Count})</span></h2>
            <button type=""button"" class=""add-btn"" onclick=""addPasskey()"">Add Passkey</button>
        </div>
        <table class=""sessions-table"">
            <thead>
                <tr>
                    <th>Name</th>
                    <th>Created</th>
                    <th>Action</th>
                </tr>
            </thead>
            <tbody>
                {BuildPasskeysHtml()}
            </tbody>
        </table>

        <h2>Passkey Challenges <span class=""session-count"">({passkeyChallenges.Count})</span></h2>
        <table class=""sessions-table"">
            <thead>
                <tr>
                    <th>Challenge</th>
                    <th>Created</th>
                    <th>Action</th>
                </tr>
            </thead>
            <tbody>
                {BuildPasskeyChallengesHtml()}
            </tbody>
        </table>
        </div>
        <script>
        async function addPasskey() {{
            const name = prompt('Enter a name for this passkey:');
            if (!name || name.trim() === '') {{
                return;
            }}

            try {{
                // Fetch registration options from server
                const optionsResponse = await fetch('/admin/passkeyregistrationoptions', {{
                    method: 'POST',
                    headers: {{ 'Content-Type': 'application/json' }},
                    body: JSON.stringify({{ name: name.trim() }})
                }});
                
                if (!optionsResponse.ok) {{
                    throw new Error('Failed to get registration options');
                }}
                
                const options = await optionsResponse.json();
                
                // Convert base64url strings to ArrayBuffers
                options.challenge = base64urlToBuffer(options.challenge);
                options.user.id = base64urlToBuffer(options.user.id);
                if (options.excludeCredentials) {{
                    options.excludeCredentials = options.excludeCredentials.map(cred => ({{
                        ...cred,
                        id: base64urlToBuffer(cred.id)
                    }}));
                }}
                
                // Create credential using WebAuthn API
                const credential = await navigator.credentials.create({{ publicKey: options }});
                
                // Prepare credential data for server
                const credentialData = {{
                    name: name.trim(),
                    id: bufferToBase64url(credential.rawId),
                    rawId: bufferToBase64url(credential.rawId),
                    type: credential.type,
                    response: {{
                        clientDataJSON: bufferToBase64url(credential.response.clientDataJSON),
                        attestationObject: bufferToBase64url(credential.response.attestationObject)
                    }}
                }};
                
                // Send credential to server
                const registerResponse = await fetch('/admin/registerpasskey', {{
                    method: 'POST',
                    headers: {{ 'Content-Type': 'application/json' }},
                    body: JSON.stringify(credentialData)
                }});
                
                if (registerResponse.ok) {{
                    window.location.reload();
                }} else {{
                    const error = await registerResponse.text();
                    alert('Failed to register passkey: ' + error);
                }}
            }} catch (err) {{
                console.error('Passkey registration error:', err);
                alert('Passkey registration failed: ' + err.message);
            }}
        }}
        
        function base64urlToBuffer(base64url) {{
            const base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
            const padding = '='.repeat((4 - base64.length % 4) % 4);
            const binary = atob(base64 + padding);
            const bytes = new Uint8Array(binary.length);
            for (let i = 0; i < binary.length; i++) {{
                bytes[i] = binary.charCodeAt(i);
            }}
            return bytes.buffer;
        }}
        
        function bufferToBase64url(buffer) {{
            const bytes = new Uint8Array(buffer);
            let binary = '';
            for (let i = 0; i < bytes.length; i++) {{
                binary += String.fromCharCode(bytes[i]);
            }}
            const base64 = btoa(binary);
            return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
        }}
        </script>
        </body>
        </html>
        ";
        return Results.Content(html, "text/html", statusCode: 200);
    }
}
