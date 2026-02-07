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

        IncrementStatistics();

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
        string safeScope = System.Net.WebUtility.HtmlEncode(XrpcHelpers.GetRequestBodyArgumentValue(oauthRequest.Body,"scope"));

        //
        // Render HTML to capture username and password, with passkey support.
        // Styling matches Admin Login page.
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
            a {{ color: #1d9bf0; text-decoration: none; }}
            a:hover {{ text-decoration: underline; }}
            label {{ display: block; margin-bottom: 6px; color: #8899a6; }}
            input[type=""text""], input[type=""password""] {{ width: 100%; padding: 12px; margin-bottom: 16px; background-color: #2f3336; border: 1px solid #3d4144; border-radius: 6px; color: #e7e9ea; font-size: 16px; box-sizing: border-box; }}
            input:focus {{ outline: none; border-color: #1d9bf0; }}
            button {{ background-color: #4caf50; color: white; border: none; padding: 12px 24px; border-radius: 6px; font-size: 16px; font-weight: bold; cursor: pointer; }}
            button:hover {{ background-color: #388e3c; }}
            .passkey-btn {{ background-color: #4caf50; width: 100%; margin-bottom: 16px; }}
            .passkey-btn:hover {{ background-color: #388e3c; }}
            .passkey-btn:disabled {{ background-color: #2f3336; color: #8899a6; cursor: not-allowed; }}
            .divider {{ display: flex; align-items: center; margin: 24px 0; color: #8899a6; }}
            .divider::before, .divider::after {{ content: ''; flex: 1; border-bottom: 1px solid #3d4144; }}
            .divider span {{ padding: 0 16px; }}
            .error-msg {{ color: #f44336; margin-bottom: 16px; display: none; }}
            .auth-failed {{ color: #f44336; margin-bottom: 16px; }}
        </style>
        </head>
        <body>
        <div class=""container"">
        <h1>Authorize {safeClientId}</h1>
        {(failed ? "<p class=\"auth-failed\">Authentication failed. Please try again.</p>" : "")}
        <p><strong>{safeClientId}</strong> is requesting access to your account.</p>
        <p>Requested permissions: <code>{safeScope}</code></p>
        
        <div id=""passkey-section"">
            <button type=""button"" id=""passkey-btn"" class=""passkey-btn"" onclick=""loginWithPasskey()"">Authorize with Passkey</button>
            <div id=""passkey-error"" class=""error-msg""></div>
        </div>
        
        <div class=""divider""><span>or</span></div>
        
        <form method=""post"" action=""/oauth/authorize"">
            <input type=""hidden"" name=""request_uri"" value=""{safeRequestUri}"" />
            <input type=""hidden"" name=""client_id"" value=""{safeClientId}"" />
            <label for=""username"">Username</label>
            <input type=""text"" id=""username"" name=""username"" />
            <label for=""password"">Password</label>
            <input type=""password"" id=""password"" name=""password"" />
            <button type=""submit"">Authorize with Password</button>
        </form>
        </div>
        <script>
        const requestUri = '{safeRequestUri}';
        const clientId = '{safeClientId}';
        
        async function loginWithPasskey() {{
            const btn = document.getElementById('passkey-btn');
            const errorDiv = document.getElementById('passkey-error');
            errorDiv.style.display = 'none';
            btn.disabled = true;
            btn.textContent = 'Authenticating...';
            
            try {{
                // Fetch authentication options from server
                const optionsResponse = await fetch('/oauth/passkeyauthenticationoptions', {{
                    method: 'POST',
                    headers: {{ 'Content-Type': 'application/json' }},
                    body: JSON.stringify({{ request_uri: requestUri, client_id: clientId }})
                }});
                
                if (!optionsResponse.ok) {{
                    const err = await optionsResponse.json();
                    throw new Error(err.error || 'Failed to get authentication options');
                }}
                
                const options = await optionsResponse.json();
                
                // Convert base64url strings to ArrayBuffers
                options.challenge = base64urlToBuffer(options.challenge);
                if (options.allowCredentials) {{
                    options.allowCredentials = options.allowCredentials.map(cred => ({{
                        ...cred,
                        id: base64urlToBuffer(cred.id)
                    }}));
                }}
                
                // Get credential using WebAuthn API
                const assertion = await navigator.credentials.get({{ publicKey: options }});
                
                // Prepare assertion data for server
                const assertionData = {{
                    id: bufferToBase64url(assertion.rawId),
                    rawId: bufferToBase64url(assertion.rawId),
                    type: assertion.type,
                    request_uri: requestUri,
                    client_id: clientId,
                    response: {{
                        clientDataJSON: bufferToBase64url(assertion.response.clientDataJSON),
                        authenticatorData: bufferToBase64url(assertion.response.authenticatorData),
                        signature: bufferToBase64url(assertion.response.signature),
                        userHandle: assertion.response.userHandle ? bufferToBase64url(assertion.response.userHandle) : null
                    }}
                }};
                
                // Send assertion to server for verification
                const authResponse = await fetch('/oauth/authenticatepasskey', {{
                    method: 'POST',
                    headers: {{ 'Content-Type': 'application/json' }},
                    body: JSON.stringify(assertionData)
                }});
                
                if (authResponse.ok) {{
                    const result = await authResponse.json();
                    if (result.redirect_url) {{
                        window.location.href = result.redirect_url;
                    }} else {{
                        throw new Error('No redirect URL in response');
                    }}
                }} else {{
                    const err = await authResponse.json();
                    throw new Error(err.error || 'Authentication failed');
                }}
            }} catch (err) {{
                console.error('Passkey authentication error:', err);
                errorDiv.textContent = err.message || 'Passkey authentication failed';
                errorDiv.style.display = 'block';
                btn.disabled = false;
                btn.textContent = 'Authorize with Passkey';
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
        
        // Check if passkeys are supported and hide button if not
        if (!window.PublicKeyCredential) {{
            document.getElementById('passkey-section').style.display = 'none';
            document.querySelector('.divider').style.display = 'none';
        }}
        </script>
        </body>
        </html>
        ";

        return html;
    }
}