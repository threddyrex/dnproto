using System.Net;
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
        // Helper functions for config display
        //
        string HtmlEncode(string value) => WebUtility.HtmlEncode(value);
        string GetConfigValue(string key) => Pds.PdsDb.ConfigPropertyExists(key) ? HtmlEncode(Pds.PdsDb.GetConfigProperty(key)) : "<span class=\"dimmed\">empty</span>";
        string GetBoolConfigValue(string key) => !Pds.PdsDb.ConfigPropertyExists(key) ? "<span class=\"dimmed\">empty</span>" : (Pds.PdsDb.GetConfigPropertyBool(key) ? "enabled" : "<span class=\"dimmed\">disabled</span>");

        //
        // return account info
        //
        string html = $@"
        <html>
        <head>
        <title>Admin - Home - {TryGetPdsHostname()}</title>
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
            {GetNavbarCss()}
            table {{ width: 100%; border-collapse: collapse; background-color: #2f3336; border-radius: 8px; overflow: hidden; margin-top: 16px; }}
            th {{ background-color: #1d1f23; color: #8899a6; text-align: left; padding: 12px 16px; font-size: 14px; font-weight: 500; }}
            td {{ padding: 10px 16px; border-bottom: 1px solid #444; font-size: 14px; }}
            tr:last-child td {{ border-bottom: none; }}
            tr:hover {{ background-color: #3a3d41; }}
            .dimmed {{ color: #657786; }}
            .key-name {{ font-weight: bold; color: #1d9bf0; }}
            .section-header td {{ background-color: #1d1f23; color: #8899a6; font-weight: 500; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px; }}
        </style>
        </head>
        <body>
        <div class=""container"">
        {GetNavbarHtml("home")}
        <h1>Admin Dashboard</h1>
        <p>
        Welcome to the dnproto PDS Admin Dashboard.
        Below is the configuration for this PDS. You can edit the config on the Config page.
        </p>

        <h2>Configuration</h2>
        <table>
            <tr>
                <th>Key</th>
                <th>Value</th>
                <th>Description</th>
            </tr>
            <tr class=""section-header""><td colspan=""3"">Server</td></tr>
            <tr>
                <td class=""key-name"">ServerListenScheme</td>
                <td>{GetConfigValue("ServerListenScheme")}</td>
                <td>http or https?</td>
            </tr>
            <tr>
                <td class=""key-name"">ServerListenHost</td>
                <td>{GetConfigValue("ServerListenHost")}</td>
                <td>Hostname that server listens on. Can be localhost for reverse proxy.</td>
            </tr>
            <tr>
                <td class=""key-name"">ServerListenPort</td>
                <td>{GetConfigValue("ServerListenPort")}</td>
                <td>Port that server listens on.</td>
            </tr>
            <tr class=""section-header""><td colspan=""3"">Features</td></tr>
            <tr>
                <td class=""key-name"">FeatureEnabled_AdminDashboard</td>
                <td>{GetBoolConfigValue("FeatureEnabled_AdminDashboard")}</td>
                <td>Is the admin dashboard enabled?</td>
            </tr>
            <tr>
                <td class=""key-name"">FeatureEnabled_Oauth</td>
                <td>{GetBoolConfigValue("FeatureEnabled_Oauth")}</td>
                <td>Is OAuth enabled? This is a global flag that turns it off/on.</td>
            </tr>
            <tr>
                <td class=""key-name"">FeatureEnabled_Passkeys</td>
                <td>{GetBoolConfigValue("FeatureEnabled_Passkeys")}</td>
                <td>Are passkeys enabled?</td>
            </tr>
            <tr>
                <td class=""key-name"">FeatureEnabled_RequestCrawl</td>
                <td>{GetBoolConfigValue("FeatureEnabled_RequestCrawl")}</td>
                <td>If enabled, will periodically request a crawl from the crawlers.</td>
            </tr>
            <tr class=""section-header""><td colspan=""3"">PDS</td></tr>
            <tr>
                <td class=""key-name"">PdsCrawlers</td>
                <td>{GetConfigValue("PdsCrawlers")}</td>
                <td>Comma-separated list of relays to request crawl from. (ex: bsky.network)</td>
            </tr>
            <tr>
                <td class=""key-name"">PdsDid</td>
                <td>{GetConfigValue("PdsDid")}</td>
                <td>DID for the PDS (ex: did:web:thisisyourpdshost.com)</td>
            </tr>
            <tr>
                <td class=""key-name"">PdsHostname</td>
                <td>{GetConfigValue("PdsHostname")}</td>
                <td>Hostname for the PDS. What goes in your DID doc.</td>
            </tr>
            <tr>
                <td class=""key-name"">PdsAvailableUserDomain</td>
                <td>{GetConfigValue("PdsAvailableUserDomain")}</td>
                <td>A single domain that is the available user domains, prefixed with .</td>
            </tr>
            <tr class=""section-header""><td colspan=""3"">User</td></tr>
            <tr>
                <td class=""key-name"">UserHandle</td>
                <td>{GetConfigValue("UserHandle")}</td>
                <td>Handle for the user (this is a single-user PDS).</td>
            </tr>
            <tr>
                <td class=""key-name"">UserDid</td>
                <td>{GetConfigValue("UserDid")}</td>
                <td>DID for the user (ex: did:web:______).</td>
            </tr>
            <tr>
                <td class=""key-name"">UserEmail</td>
                <td>{GetConfigValue("UserEmail")}</td>
                <td>User's email address.</td>
            </tr>
            <tr>
                <td class=""key-name"">UserIsActive</td>
                <td>{GetBoolConfigValue("UserIsActive")}</td>
                <td>Is the user active?</td>
            </tr>
            <tr class=""section-header""><td colspan=""3"">Deployment</td></tr>
            <tr>
                <td class=""key-name"">LogRetentionDays</td>
                <td>{GetConfigValue("LogRetentionDays")}</td>
                <td>Number of days to keep logs before deleting.</td>
            </tr>
            <tr>
                <td class=""key-name"">SystemctlServiceName</td>
                <td>{GetConfigValue("SystemctlServiceName")}</td>
                <td>systemctl service name. Gets used during deployment/restart.</td>
            </tr>
            <tr>
                <td class=""key-name"">CaddyAccessLogFilePath</td>
                <td>{GetConfigValue("CaddyAccessLogFilePath")}</td>
                <td>Access log for caddy.</td>
            </tr>
        </table>
        </div>
        </body>
        </html>
        ";
        return Results.Content(html, "text/html", statusCode: 200);
    }





}