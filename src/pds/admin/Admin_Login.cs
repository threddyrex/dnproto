using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// </summary>
public class Admin_Login : BaseAdmin
{
    public IResult GetResponse()
    {
        IncrementStatistics();
        
        if(AdminInterfaceIsEnabled() == false)
        {
            return Results.StatusCode(404);
        }


        //
        // GET
        //
        if(HttpContext.Request.Method == "GET")
        {
            // render html for login page, that looks similar to the oauth auth page
            string html = @"
            <html>
            <head>
            <title>Login Required</title>
            <style>
                body { background-color: #16181c; color: #e7e9ea; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; padding: 40px 20px; }
                .container { max-width: 500px; margin: 0 0 0 40px; }
                h1 { color: #8899a6; margin-bottom: 24px; }
                p { margin-bottom: 16px; line-height: 1.5; }
                code { background-color: #2f3336; padding: 2px 6px; border-radius: 4px; }
                a { color: #1d9bf0; text-decoration: none; }
                a:hover { text-decoration: underline; }
                label { display: block; margin-bottom: 6px; color: #8899a6; }
                input[type=""text""], input[type=""password""] { width: 100%; padding: 12px; margin-bottom: 16px; background-color: #2f3336; border: 1px solid #3d4144; border-radius: 6px; color: #e7e9ea; font-size: 16px; box-sizing: border-box; }
                input:focus { outline: none; border-color: #1d9bf0; }
                button { background-color: #4caf50; color: white; border: none; padding: 12px 24px; border-radius: 6px; font-size: 16px; font-weight: bold; cursor: pointer; }
                button:hover { background-color: #388e3c; }
                .passkey-btn { background-color: #4caf50; width: 100%; margin-bottom: 16px; }
                .passkey-btn:hover { background-color: #388e3c; }
                .passkey-btn:disabled { background-color: #2f3336; color: #8899a6; cursor: not-allowed; }
                .divider { display: flex; align-items: center; margin: 24px 0; color: #8899a6; }
                .divider::before, .divider::after { content: ''; flex: 1; border-bottom: 1px solid #3d4144; }
                .divider span { padding: 0 16px; }
                .error-msg { color: #f44336; margin-bottom: 16px; display: none; }
            </style>
            </head>
            <body>
            <div class=""container"">
            <h1>Login Required</h1>
            <p>You must be logged in to access account information.</p>
            
            <div id=""passkey-section"">
                <button type=""button"" id=""passkey-btn"" class=""passkey-btn"" onclick=""loginWithPasskey()"">Log in with Passkey</button>
                <div id=""passkey-error"" class=""error-msg""></div>
            </div>
            
            <div class=""divider""><span>or</span></div>
            
            <form method=""post"" action=""/admin/login"">
                <label for=""username"">Username</label>
                <input type=""text"" id=""username"" name=""username"" />
                <label for=""password"">Password</label>
                <input type=""password"" id=""password"" name=""password"" />
                <button type=""submit"">Log in with Password</button>
            </form>
            </div>
            <script>
            async function loginWithPasskey() {
                const btn = document.getElementById('passkey-btn');
                const errorDiv = document.getElementById('passkey-error');
                errorDiv.style.display = 'none';
                btn.disabled = true;
                btn.textContent = 'Authenticating...';
                
                try {
                    // Fetch authentication options from server
                    const optionsResponse = await fetch('/admin/passkeyauthenticationoptions', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' }
                    });
                    
                    if (!optionsResponse.ok) {
                        const err = await optionsResponse.json();
                        throw new Error(err.error || 'Failed to get authentication options');
                    }
                    
                    const options = await optionsResponse.json();
                    
                    // Convert base64url strings to ArrayBuffers
                    options.challenge = base64urlToBuffer(options.challenge);
                    if (options.allowCredentials) {
                        options.allowCredentials = options.allowCredentials.map(cred => ({
                            ...cred,
                            id: base64urlToBuffer(cred.id)
                        }));
                    }
                    
                    // Get credential using WebAuthn API
                    const assertion = await navigator.credentials.get({ publicKey: options });
                    
                    // Prepare assertion data for server
                    const assertionData = {
                        id: bufferToBase64url(assertion.rawId),
                        rawId: bufferToBase64url(assertion.rawId),
                        type: assertion.type,
                        response: {
                            clientDataJSON: bufferToBase64url(assertion.response.clientDataJSON),
                            authenticatorData: bufferToBase64url(assertion.response.authenticatorData),
                            signature: bufferToBase64url(assertion.response.signature),
                            userHandle: assertion.response.userHandle ? bufferToBase64url(assertion.response.userHandle) : null
                        }
                    };
                    
                    // Send assertion to server for verification
                    const authResponse = await fetch('/admin/authenticatepasskey', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(assertionData)
                    });
                    
                    if (authResponse.ok) {
                        window.location.href = '/admin/';
                    } else {
                        const err = await authResponse.json();
                        throw new Error(err.error || 'Authentication failed');
                    }
                } catch (err) {
                    console.error('Passkey authentication error:', err);
                    errorDiv.textContent = err.message || 'Passkey authentication failed';
                    errorDiv.style.display = 'block';
                    btn.disabled = false;
                    btn.textContent = 'Log in with Passkey';
                }
            }
            
            function base64urlToBuffer(base64url) {
                const base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
                const padding = '='.repeat((4 - base64.length % 4) % 4);
                const binary = atob(base64 + padding);
                const bytes = new Uint8Array(binary.length);
                for (let i = 0; i < binary.length; i++) {
                    bytes[i] = binary.charCodeAt(i);
                }
                return bytes.buffer;
            }
            
            function bufferToBase64url(buffer) {
                const bytes = new Uint8Array(buffer);
                let binary = '';
                for (let i = 0; i < bytes.length; i++) {
                    binary += String.fromCharCode(bytes[i]);
                }
                const base64 = btoa(binary);
                return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
            }
            
            // Check if passkeys are supported and hide button if not
            if (!window.PublicKeyCredential) {
                document.getElementById('passkey-section').style.display = 'none';
                document.querySelector('.divider').style.display = 'none';
            }
            </script>
            </body>
            </html>"
            ;
            return Results.Content(html, "text/html", statusCode: 200);
        }



        //
        // Handle POST (login) requests
        //
        if(HttpContext.Request.Method == "POST")
        {
            var form = HttpContext.Request.Form;
            string? userName = form["username"];
            string? password = form["password"];

            bool actorCorrect = userName != null && userName == "admin";
            string? storedHashedPassword = Pds.PdsDb.GetConfigProperty("AdminHashedPassword");
            bool passwordMatches = PasswordHasher.VerifyPassword(storedHashedPassword, password);
            bool authSucceeded = actorCorrect && passwordMatches;


            // validate credentials
            if(authSucceeded)
            {
                //
                // Create admin session and insert into db
                //
                var adminSession = new AdminSession()
                {
                    SessionId = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                    IpAddress = GetCallerIpAddress(),
                    CreatedDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow),
                    UserAgent = GetCallerUserAgent() ?? "unknown",
                    AuthType = "Legacy"
                };

                Pds.PdsDb.InsertAdminSession(adminSession);

                Pds.Logger.LogInfo($"[AUTH] [ADMIN] authSucceeded={authSucceeded} ip={adminSession.IpAddress}");

                //
                // set cookie with session id
                //
                HttpContext.Response.Cookies.Append("adminSessionId", adminSession.SessionId, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddHours(1)
                });

                //
                // redirect to /admin
                //
                HttpContext.Response.Redirect("/admin/");
                return Results.Empty;
            }
            else
            {
                // redirect back to /admin/login
                HttpContext.Response.Redirect("/admin/login");
                return Results.Empty;
            }
        }


        // method not allowed
        return Results.StatusCode(405);
    }





}