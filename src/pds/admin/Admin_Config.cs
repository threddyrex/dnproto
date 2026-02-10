using System.Security.Claims;
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
            string? key = HttpContext.Request.Form["key"];
            string? value = HttpContext.Request.Form["value"];
            if(!string.IsNullOrEmpty(key) && value != null)
            {
                Pds.PdsDb.SetConfigProperty(key, value);
            }
        }


        //
        // return config page
        //
        string GetConfigValueRaw(string key) => Pds.PdsDb.ConfigPropertyExists(key) ? Pds.PdsDb.GetConfigProperty(key) : "";
        string GetConfigValue(string key) => Pds.PdsDb.ConfigPropertyExists(key) ? Pds.PdsDb.GetConfigProperty(key) : "<span class=\"dimmed\">empty</span>";
        string GetBoolConfigValue(string key) => !Pds.PdsDb.ConfigPropertyExists(key) ? "<span class=\"dimmed\">empty</span>" : (Pds.PdsDb.GetConfigPropertyBool(key) ? "enabled" : "<span class=\"dimmed\">disabled</span>");

        string html = $@"
        <html>
        <head>
        <title>Admin - Config</title>
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
            table {{ border-collapse: collapse; width: 100%; margin-top: 16px; }}
            th, td {{ border: 1px solid #2f3336; padding: 12px; text-align: left; }}
            th {{ background-color: #22303c; color: #8899a6; }}
            td {{ background-color: #192734; }}
            .set-btn {{ background-color: #1d9bf0; color: white; border: none; padding: 6px 12px; border-radius: 4px; cursor: pointer; font-size: 13px; }}
            .set-btn:hover {{ background-color: #1a8cd8; }}
            .enable-btn {{ background-color: #4caf50; color: white; border: none; padding: 6px 12px; border-radius: 4px; cursor: pointer; font-size: 13px; margin-right: 6px; }}
            .enable-btn:hover {{ background-color: #388e3c; }}
            .disable-btn {{ background-color: #f44336; color: white; border: none; padding: 6px 12px; border-radius: 4px; cursor: pointer; font-size: 13px; }}
            .disable-btn:hover {{ background-color: #d32f2f; }}
            .dimmed {{ color: #657786; }}
        </style>
        </head>
        <body>
        <div class=""container"">
        <div class=""navbar"">
            <a href=""/admin/"" class=""nav-btn"">Home</a>
            <a href=""/admin/config"" class=""nav-btn active"">Config</a>
            <a href=""/admin/passkeys"" class=""nav-btn"">Passkeys</a>
            <a href=""/admin/sessions"" class=""nav-btn"">Sessions</a>
            <a href=""/admin/stats"" class=""nav-btn"">Statistics</a>
            <form method=""post"" action=""/admin/logout"" style=""margin: 0;"">
                <button type=""submit"" class=""logout-btn"">Logout</button>
            </form>
        </div>
        <h1>Config</h1>
        <table>
            <tr>
                <th>Key</th>
                <th>Value</th>
                <th>Action</th>
            </tr>
            <tr>
                <td>ServerListenScheme</td>
                <td>{GetConfigValue("ServerListenScheme")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('ServerListenScheme', '{GetConfigValueRaw("ServerListenScheme")}')"">Set</button></td>
            </tr>
            <tr>
                <td>ServerListenHost</td>
                <td>{GetConfigValue("ServerListenHost")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('ServerListenHost', '{GetConfigValueRaw("ServerListenHost")}')"">Set</button></td>
            </tr>
            <tr>
                <td>ServerListenPort</td>
                <td>{GetConfigValue("ServerListenPort")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('ServerListenPort', '{GetConfigValueRaw("ServerListenPort")}')"">Set</button></td>
            </tr>
            <tr>
                <td>FeatureEnabled_AdminDashboard</td>
                <td>{GetBoolConfigValue("FeatureEnabled_AdminDashboard")}</td>
                <td><button class=""enable-btn"" onclick=""setBoolConfig('FeatureEnabled_AdminDashboard', '1')"">Enable</button><button class=""disable-btn"" onclick=""setBoolConfig('FeatureEnabled_AdminDashboard', '0')"">Disable</button></td>
            </tr>
            <tr>
                <td>FeatureEnabled_Oauth</td>
                <td>{GetBoolConfigValue("FeatureEnabled_Oauth")}</td>
                <td><button class=""enable-btn"" onclick=""setBoolConfig('FeatureEnabled_Oauth', '1')"">Enable</button><button class=""disable-btn"" onclick=""setBoolConfig('FeatureEnabled_Oauth', '0')"">Disable</button></td>
            </tr>
            <tr>
                <td>FeatureEnabled_RequestCrawl</td>
                <td>{GetBoolConfigValue("FeatureEnabled_RequestCrawl")}</td>
                <td><button class=""enable-btn"" onclick=""setBoolConfig('FeatureEnabled_RequestCrawl', '1')"">Enable</button><button class=""disable-btn"" onclick=""setBoolConfig('FeatureEnabled_RequestCrawl', '0')"">Disable</button></td>
            </tr>
            <tr>
                <td>FeatureEnabled_Passkeys</td>
                <td>{GetBoolConfigValue("FeatureEnabled_Passkeys")}</td>
                <td><button class=""enable-btn"" onclick=""setBoolConfig('FeatureEnabled_Passkeys', '1')"">Enable</button><button class=""disable-btn"" onclick=""setBoolConfig('FeatureEnabled_Passkeys', '0')"">Disable</button></td>
            </tr>
            <tr>
                <td>PdsCrawlers</td>
                <td>{GetConfigValue("PdsCrawlers")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('PdsCrawlers', '{GetConfigValueRaw("PdsCrawlers")}')"">Set</button></td>
            </tr>
            <tr>
                <td>PdsDid</td>
                <td>{GetConfigValue("PdsDid")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('PdsDid', '{GetConfigValueRaw("PdsDid")}')"">Set</button></td>
            </tr>
            <tr>
                <td>PdsHostname</td>
                <td>{GetConfigValue("PdsHostname")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('PdsHostname', '{GetConfigValueRaw("PdsHostname")}')"">Set</button></td>
            </tr>
            <tr>
                <td>PdsAvailableUserDomain</td>
                <td>{GetConfigValue("PdsAvailableUserDomain")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('PdsAvailableUserDomain', '{GetConfigValueRaw("PdsAvailableUserDomain")}')"">Set</button></td>
            </tr>
            <tr>
                <td>UserHandle</td>
                <td>{GetConfigValue("UserHandle")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('UserHandle', '{GetConfigValueRaw("UserHandle")}')"">Set</button></td>
            </tr>
            <tr>
                <td>UserDid</td>
                <td>{GetConfigValue("UserDid")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('UserDid', '{GetConfigValueRaw("UserDid")}')"">Set</button></td>
            </tr>
            <tr>
                <td>UserEmail</td>
                <td>{GetConfigValue("UserEmail")}</td>
                <td><button class=""set-btn"" onclick=""setConfig('UserEmail', '{GetConfigValueRaw("UserEmail")}')"">Set</button></td>
            </tr>
            <tr>
                <td>UserIsActive</td>
                <td>{GetBoolConfigValue("UserIsActive")}</td>
                <td><button class=""enable-btn"" onclick=""setBoolConfig('UserIsActive', '1')"">Enable</button><button class=""disable-btn"" onclick=""setBoolConfig('UserIsActive', '0')"">Disable</button></td>
            </tr>
        </table>
        <script>
        function setConfig(key, currentValue) {{
            var newValue = prompt('Enter new value for ' + key + ':', currentValue);
            if (newValue !== null) {{
                var form = document.createElement('form');
                form.method = 'POST';
                form.action = '/admin/config';
                var keyInput = document.createElement('input');
                keyInput.type = 'hidden';
                keyInput.name = 'key';
                keyInput.value = key;
                var valueInput = document.createElement('input');
                valueInput.type = 'hidden';
                valueInput.name = 'value';
                valueInput.value = newValue;
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
            var keyInput = document.createElement('input');
            keyInput.type = 'hidden';
            keyInput.name = 'key';
            keyInput.value = key;
            var valueInput = document.createElement('input');
            valueInput.type = 'hidden';
            valueInput.name = 'value';
            valueInput.value = value;
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
