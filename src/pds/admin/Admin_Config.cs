using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// Config page for admin interface
/// </summary>
public class Admin_Config : BaseAdmin
{
    // Whitelist of allowed config keys
    private static readonly HashSet<string> AllowedConfigKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ServerListenScheme",
        "ServerListenHost",
        "ServerListenPort",
        "FeatureEnabled_AdminDashboard",
        "FeatureEnabled_Oauth",
        "FeatureEnabled_Passkeys",
        "PdsCrawlers",
        "PdsDid",
        "PdsHostname",
        "PdsAvailableUserDomain",
        "UserHandle",
        "UserDid",
        "UserEmail",
        "UserIsActive",
        "LogRetentionDays",
        "SystemctlServiceName",
        "CaddyAccessLogFilePath",
        "FeatureEnabled_RequestCrawl",
        "AtprotoProxyAllowedDids"
    };

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


        //
        // Handle POST to set config property
        //
        if(HttpContext.Request.Method == "POST")
        {
            // Validate CSRF token
            string? submittedCsrfToken = HttpContext.Request.Form["csrf_token"];
            string? csrfCookie = null;
            HttpContext.Request.Cookies.TryGetValue("csrf_token", out csrfCookie);
            
            if(string.IsNullOrEmpty(submittedCsrfToken) || string.IsNullOrEmpty(csrfCookie) || 
               !CryptographicOperations.FixedTimeEquals(
                   System.Text.Encoding.UTF8.GetBytes(submittedCsrfToken),
                   System.Text.Encoding.UTF8.GetBytes(csrfCookie)))
            {
                return Results.StatusCode(403); // CSRF validation failed
            }

            string? key = HttpContext.Request.Form["key"];
            string? value = HttpContext.Request.Form["value"];
            
            // Only allow whitelisted keys
            if(!string.IsNullOrEmpty(key) && value != null && AllowedConfigKeys.Contains(key))
            {
                Pds.PdsDb.SetConfigProperty(key, value);
            }
            
            // POST-Redirect-GET pattern to prevent form resubmission
            HttpContext.Response.Redirect("/admin/config");
            return Results.Empty;
        }


        //
        // Generate CSRF token and set as cookie
        //
        string csrfToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        HttpContext.Response.Cookies.Append("csrf_token", csrfToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromHours(1)
        });

        //
        // return config page
        //
        // HTML-encode values for safe display, JS-escape for onclick handlers
        string HtmlEncode(string value) => WebUtility.HtmlEncode(value);
        string JsEscape(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r");
        
        string GetConfigValueRaw(string key) => Pds.PdsDb.ConfigPropertyExists(key) ? Pds.PdsDb.GetConfigProperty(key) : "";
        string GetConfigValue(string key) => Pds.PdsDb.ConfigPropertyExists(key) ? HtmlEncode(Pds.PdsDb.GetConfigProperty(key)) : "<span class=\"dimmed\">empty</span>";
        string GetConfigValueForJs(string key) => JsEscape(GetConfigValueRaw(key));
        string GetBoolConfigValue(string key) => !Pds.PdsDb.ConfigPropertyExists(key) ? "<span class=\"dimmed\">empty</span>" : (Pds.PdsDb.GetConfigPropertyBool(key) ? "enabled" : "<span class=\"dimmed\">disabled</span>");

        string html = $@"
        <html>
        <head>
        <title>Admin - Config - {TryGetPdsHostname()}</title>
        <style>
            body {{ background-color: #16181c; color: #e7e9ea; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; padding: 40px 20px; }}
            .container {{ max-width: 800px; margin: 0 0 0 40px; }}
            h1 {{ color: #8899a6; margin-bottom: 24px; }}
            h2 {{ color: #8899a6; margin-top: 32px; margin-bottom: 16px; font-size: 18px; }}
            {GetNavbarCss()}
            table {{ width: 100%; border-collapse: collapse; background-color: #2f3336; border-radius: 8px; overflow: hidden; margin-top: 16px; }}
            th {{ background-color: #1d1f23; color: #8899a6; text-align: left; padding: 12px 16px; font-size: 14px; font-weight: 500; }}
            td {{ padding: 10px 16px; border-bottom: 1px solid #444; font-size: 14px; }}
            tr:last-child td {{ border-bottom: none; }}
            tr:hover {{ background-color: #3a3d41; }}
            .set-btn {{ background-color: #f44336; color: white; border: none; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; font-weight: 500; }}
            .set-btn:hover {{ background-color: #d32f2f; }}
            .enable-btn {{ background-color: #f44336; color: white; border: none; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; font-weight: 500; margin-bottom: 8px; display: block; }}
            .enable-btn:hover {{ background-color: #d32f2f; }}
            .disable-btn {{ background-color: #f44336; color: white; border: none; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; font-weight: 500; }}
            .disable-btn:hover {{ background-color: #d32f2f; }}
            .dimmed {{ color: #657786; }}
            .key-name {{ font-weight: bold; color: #1d9bf0; }}
            .section-header td {{ background-color: #1d1f23; color: #8899a6; font-weight: 500; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px; }}
        </style>
        </head>
        <body>
        <div class=""container"">
        {GetNavbarHtml("config")}
        <h1>Config</h1>

        <table>
            <tr>
                <th>Key</th>
                <th>Value</th>
                <th>Action</th>
                <th>Description</th>
            </tr>
            <tr class=""section-header""><td colspan=""4"">Server</td></tr>
            <tr>
                <td class=""key-name"">ServerListenScheme</td>
                <td>{GetConfigValue("ServerListenScheme")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('ServerListenScheme', '{GetConfigValueForJs("ServerListenScheme")}')"">Set</button></td>
                <td>http or https?</td>
            </tr>
            <tr>
                <td class=""key-name"">ServerListenHost</td>
                <td>{GetConfigValue("ServerListenHost")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('ServerListenHost', '{GetConfigValueForJs("ServerListenHost")}')"">Set</button></td>
                <td>Hostname that server listens on. Can be localhost for reverse proxy.</td>
            </tr>
            <tr>
                <td class=""key-name"">ServerListenPort</td>
                <td>{GetConfigValue("ServerListenPort")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('ServerListenPort', '{GetConfigValueForJs("ServerListenPort")}')"">Set</button></td>
                <td>Port that server listens on.</td>
            </tr>
            <tr class=""section-header""><td colspan=""4"">Features</td></tr>
            <tr>
                <td class=""key-name"">FeatureEnabled_AdminDashboard</td>
                <td>{GetBoolConfigValue("FeatureEnabled_AdminDashboard")}</td>
                <td><button class=""enable-btn"" onclick=""setBoolConfig('FeatureEnabled_AdminDashboard', '1')"">Enable</button><button class=""disable-btn"" onclick=""if(confirm('WARNING: Disabling the admin dashboard will lock you out immediately. Are you sure?')) setBoolConfig('FeatureEnabled_AdminDashboard', '0')"">Disable</button></td>
                <td>Is the admin dashboard enabled?</td>
            </tr>
            <tr>
                <td class=""key-name"">FeatureEnabled_Oauth</td>
                <td>{GetBoolConfigValue("FeatureEnabled_Oauth")}</td>
                <td><button class=""enable-btn"" onclick=""setBoolConfig('FeatureEnabled_Oauth', '1')"">Enable</button><button class=""disable-btn"" onclick=""setBoolConfig('FeatureEnabled_Oauth', '0')"">Disable</button></td>
                <td>Is OAuth enabled? This is a global flag that turns it off/on.</td>
            </tr>
            <tr>
                <td class=""key-name"">FeatureEnabled_Passkeys</td>
                <td>{GetBoolConfigValue("FeatureEnabled_Passkeys")}</td>
                <td><button class=""enable-btn"" onclick=""setBoolConfig('FeatureEnabled_Passkeys', '1')"">Enable</button><button class=""disable-btn"" onclick=""setBoolConfig('FeatureEnabled_Passkeys', '0')"">Disable</button></td>
                <td>Are passkeys enabled?</td>
            </tr>
            <tr>
                <td class=""key-name"">FeatureEnabled_RequestCrawl</td>
                <td>{GetBoolConfigValue("FeatureEnabled_RequestCrawl")}</td>
                <td><button class=""enable-btn"" onclick=""setBoolConfig('FeatureEnabled_RequestCrawl', '1')"">Enable</button><button class=""disable-btn"" onclick=""setBoolConfig('FeatureEnabled_RequestCrawl', '0')"">Disable</button></td>
                <td>If enabled, will periodically request a crawl from the crawlers. Enable this last - things need to be configured correctly before connecting with the larger network.</td>
            </tr>
            <tr class=""section-header""><td colspan=""4"">PDS</td></tr>
            <tr>
                <td class=""key-name"">PdsCrawlers</td>
                <td>{GetConfigValue("PdsCrawlers")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('PdsCrawlers', '{GetConfigValueForJs("PdsCrawlers")}')"">Set</button></td>
                <td>Comma-separated list of relays to request crawl from. (ex: bsky.network)</td>
            </tr>
            <tr>
                <td class=""key-name"">PdsDid</td>
                <td>{GetConfigValue("PdsDid")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('PdsDid', '{GetConfigValueForJs("PdsDid")}')"">Set</button></td>
                <td>DID for the PDS (ex: did:web:thisisyourpdshost.com)</td>
            </tr>
            <tr>
                <td class=""key-name"">PdsHostname</td>
                <td>{GetConfigValue("PdsHostname")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('PdsHostname', '{GetConfigValueForJs("PdsHostname")}')"">Set</button></td>
                <td>Hostname for the PDS. What goes in your DID doc.</td>
            </tr>
            <tr>
                <td class=""key-name"">PdsAvailableUserDomain</td>
                <td>{GetConfigValue("PdsAvailableUserDomain")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('PdsAvailableUserDomain', '{GetConfigValueForJs("PdsAvailableUserDomain")}')"">Set</button></td>
                <td>A single domain that is the available user domains, prefixed with .</td>
            </tr>
            <tr class=""section-header""><td colspan=""4"">User</td></tr>
            <tr>
                <td class=""key-name"">UserHandle</td>
                <td>{GetConfigValue("UserHandle")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('UserHandle', '{GetConfigValueForJs("UserHandle")}')"">Set</button></td>
                <td>Handle for the user (this is a single-user PDS).</td>
            </tr>
            <tr>
                <td class=""key-name"">UserDid</td>
                <td>{GetConfigValue("UserDid")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('UserDid', '{GetConfigValueForJs("UserDid")}')"">Set</button></td>
                <td>DID for the user (ex: did:web:______).</td>
            </tr>
            <tr>
                <td class=""key-name"">UserEmail</td>
                <td>{GetConfigValue("UserEmail")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('UserEmail', '{GetConfigValueForJs("UserEmail")}')"">Set</button></td>
                <td>User's email address.</td>
            </tr>
            <tr>
                <td class=""key-name"">UserIsActive</td>
                <td>{GetBoolConfigValue("UserIsActive")}</td>
                <td></td>
                <td>Is the user active? You can change this on the Actions page with 'Activate' and 'Deactivate' buttons.</td>
            </tr>
            <tr class=""section-header""><td colspan=""4"">Deployment</td></tr>
            <tr>
                <td class=""key-name"">LogRetentionDays</td>
                <td>{GetConfigValue("LogRetentionDays")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('LogRetentionDays', '{GetConfigValueForJs("LogRetentionDays")}')"">Set</button></td>
                <td>Number of days to keep logs before deleting.</td>
            </tr>
            <tr>
                <td class=""key-name"">SystemctlServiceName</td>
                <td>{GetConfigValue("SystemctlServiceName")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('SystemctlServiceName', '{GetConfigValueForJs("SystemctlServiceName")}')"">Set</button></td>
                <td>systemctl service name. Gets used during deployment/restart.</td>
            </tr>
            <tr>
                <td class=""key-name"">CaddyAccessLogFilePath</td>
                <td>{GetConfigValue("CaddyAccessLogFilePath")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('CaddyAccessLogFilePath', '{GetConfigValueForJs("CaddyAccessLogFilePath")}')"">Set</button></td>
                <td>Access log for caddy.</td>
            </tr>
            <tr class=""section-header""><td colspan=""4"">Security</td></tr>
            <tr>
                <td class=""key-name"">AtprotoProxyAllowedDids</td>
                <td>{GetConfigValue("AtprotoProxyAllowedDids")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('AtprotoProxyAllowedDids', '{GetConfigValueForJs("AtprotoProxyAllowedDids")}')"">Set</button></td>
                <td>Comma-separated list of DIDs allowed for Atproto-Proxy header (SSRF protection).</td>
            </tr>
        </table>
        <script>
        function getCsrfToken() {{
            return '{csrfToken}';
        }}
        function setConfig(key, currentValue) {{
            var newValue = prompt('Enter new value for ' + key + ':', currentValue);
            if (newValue !== null) {{
                var form = document.createElement('form');
                form.method = 'POST';
                form.action = '/admin/config';
                var csrfInput = document.createElement('input');
                csrfInput.type = 'hidden';
                csrfInput.name = 'csrf_token';
                csrfInput.value = getCsrfToken();
                var keyInput = document.createElement('input');
                keyInput.type = 'hidden';
                keyInput.name = 'key';
                keyInput.value = key;
                var valueInput = document.createElement('input');
                valueInput.type = 'hidden';
                valueInput.name = 'value';
                valueInput.value = newValue;
                form.appendChild(csrfInput);
                form.appendChild(keyInput);
                form.appendChild(valueInput);
                document.body.appendChild(form);
                form.submit();
            }}
        }}
        function setBoolConfig(key, value) {{
            var form = document.createElement('form');
            form.method = 'POST';
            form.action = '/admin/config';
            var csrfInput = document.createElement('input');
            csrfInput.type = 'hidden';
            csrfInput.name = 'csrf_token';
            csrfInput.value = getCsrfToken();
            var keyInput = document.createElement('input');
            keyInput.type = 'hidden';
            keyInput.name = 'key';
            keyInput.value = key;
            var valueInput = document.createElement('input');
            valueInput.type = 'hidden';
            valueInput.name = 'value';
            valueInput.value = value;
            form.appendChild(csrfInput);
            form.appendChild(keyInput);
            form.appendChild(valueInput);
            document.body.appendChild(form);
            form.submit();
        }}
        </script>
        </div>
        </body>
        </html>
        ";
        return Results.Content(html, "text/html", statusCode: 200);
    }
}
