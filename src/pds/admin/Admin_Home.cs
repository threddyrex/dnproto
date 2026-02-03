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
        var passkeys = Pds.PdsDb.GetAllPasskeys();
        var passkeyChallenges = Pds.PdsDb.GetAllPasskeyChallenges();

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
                sb.Append($@"<div class=""session-item"">
                    <span class=""session-label"">Created:</span> {System.Net.WebUtility.HtmlEncode(s.CreatedDate)} 
                    <span class=""session-label"">IP:</span> {System.Net.WebUtility.HtmlEncode(s.IpAddress)} 
                    <span class=""session-label"">User-Agent:</span> {System.Net.WebUtility.HtmlEncode(s.UserAgent)}
                    <form method=""post"" action=""/admin/deletelegacysession"" style=""display:inline; margin-left: 12px;"">
                        <input type=""hidden"" name=""refreshJwt"" value=""{System.Net.WebUtility.HtmlEncode(s.RefreshJwt)}"" />
                        <button type=""submit"" class=""delete-btn"">Delete</button>
                    </form>
                </div>");
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
                sb.Append($@"<div class=""session-item"">
                    <span class=""session-label"">Created:</span> {System.Net.WebUtility.HtmlEncode(s.CreatedDate)} 
                    <span class=""session-label"">IP:</span> {System.Net.WebUtility.HtmlEncode(s.IpAddress)} 
                    <span class=""session-label"">User-Agent:</span> {System.Net.WebUtility.HtmlEncode(s.UserAgent)} 
                    <span class=""session-label"">Session:</span> {System.Net.WebUtility.HtmlEncode(s.SessionId)}
                    <form method=""post"" action=""/admin/deleteadminsession"" style=""display:inline; margin-left: 12px;"">
                        <input type=""hidden"" name=""sessionId"" value=""{System.Net.WebUtility.HtmlEncode(s.SessionId)}"" />
                        <button type=""submit"" class=""delete-btn"">Delete</button>
                    </form>
                </div>");
            }
            return sb.ToString();
        }

        string BuildPasskeysHtml()
        {
            if (passkeys.Count == 0)
                return "<div class=\"session-item\">No passkeys</div>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var p in passkeys)
            {
                sb.Append($@"<div class=""session-item"">
                    <span class=""session-label"">Name:</span> {System.Net.WebUtility.HtmlEncode(p.Name)} 
                    <span class=""session-label"">Created:</span> {System.Net.WebUtility.HtmlEncode(p.CreatedDate)} 
                    <span class=""session-label"">Credential ID:</span> {System.Net.WebUtility.HtmlEncode(p.CredentialId)}
                    <form method=""post"" action=""/admin/deletepasskey"" style=""display:inline; margin-left: 12px;"">
                        <input type=""hidden"" name=""name"" value=""{System.Net.WebUtility.HtmlEncode(p.Name)}"" />
                        <button type=""submit"" class=""delete-btn"">Delete</button>
                    </form>
                </div>");
            }
            return sb.ToString();
        }

        string BuildPasskeyChallengesHtml()
        {
            if (passkeyChallenges.Count == 0)
                return "<div class=\"session-item\">No passkey challenges</div>";
            
            var sb = new System.Text.StringBuilder();
            foreach (var c in passkeyChallenges)
            {
                sb.Append($@"<div class=""session-item"">
                    <span class=""session-label"">Challenge:</span> {System.Net.WebUtility.HtmlEncode(c.Challenge)} 
                    <span class=""session-label"">Created:</span> {System.Net.WebUtility.HtmlEncode(c.CreatedDate)}
                    <form method=""post"" action=""/admin/deletepasskeychallenge"" style=""display:inline; margin-left: 12px;"">
                        <input type=""hidden"" name=""challenge"" value=""{System.Net.WebUtility.HtmlEncode(c.Challenge)}"" />
                        <button type=""submit"" class=""delete-btn"">Delete</button>
                    </form>
                </div>");
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
            .info-card {{ background-color: #2f3336; border-radius: 8px; padding: 12px 16px; margin-bottom: 8px; }}
            .label {{ color: #8899a6; font-size: 14px; }}
            .value {{ color: #1d9bf0; font-size: 14px; word-break: break-all; }}
            .session-list {{ background-color: #2f3336; border-radius: 8px; padding: 16px; margin-bottom: 16px; }}
            .session-item {{ padding: 8px 0; border-bottom: 1px solid #444; font-size: 14px; }}
            .session-item:last-child {{ border-bottom: none; }}
            .session-label {{ color: #8899a6; margin-right: 4px; }}
            .session-count {{ color: #8899a6; font-size: 14px; margin-left: 8px; }}
            .logout-btn {{ background-color: #f44336; color: white; border: none; padding: 10px 20px; border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 500; }}
            .logout-btn:hover {{ background-color: #d32f2f; }}
            .delete-btn {{ background-color: #f44336; color: white; border: none; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; font-weight: 500; }}
            .delete-btn:hover {{ background-color: #d32f2f; }}
            .add-btn {{ background-color: #4caf50; color: white; border: none; padding: 8px 16px; border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 500; }}
            .add-btn:hover {{ background-color: #388e3c; }}
            .section-header {{ display: flex; justify-content: space-between; align-items: center; }}
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
            <span class=""label"">admin session id:</span> <span class=""value"">{System.Net.WebUtility.HtmlEncode(adminSession?.SessionId ?? "N/A")}</span>
        </div>
        <div class=""info-card"">
            <span class=""label"">admin session created:</span> <span class=""value"">{System.Net.WebUtility.HtmlEncode(adminSession?.CreatedDate ?? "N/A")}</span>
        </div>
        <div class=""info-card"">
            <span class=""label"">pds did:</span> <span class=""value"">{System.Net.WebUtility.HtmlEncode(Pds.Config.PdsDid)}</span>
        </div>
        <div class=""info-card"">
            <span class=""label"">user did:</span> <span class=""value"">{System.Net.WebUtility.HtmlEncode(Pds.Config.UserDid)}</span>
        </div>
        <div class=""info-card"">
            <span class=""label"">user handle:</span> <span class=""value"">{System.Net.WebUtility.HtmlEncode(Pds.Config.UserHandle)}</span>
        </div>
        <div class=""info-card"">
            <span class=""label"">user email:</span> <span class=""value"">{System.Net.WebUtility.HtmlEncode(Pds.Config.UserEmail)}</span>
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

        <div class=""section-header"">
            <h2>Passkeys <span class=""session-count"">({passkeys.Count})</span></h2>
            <button type=""button"" class=""add-btn"" onclick=""addPasskey()"">Add Passkey</button>
        </div>
        <div class=""session-list"">
            {BuildPasskeysHtml()}
        </div>

        <h2>Passkey Challenges <span class=""session-count"">({passkeyChallenges.Count})</span></h2>
        <div class=""session-list"">
            {BuildPasskeyChallengesHtml()}
        </div>
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