using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// Actions page for admin interface
/// </summary>
public class Admin_Actions : BaseAdmin
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
        // Handle POST to generate key pair
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

            string? action = HttpContext.Request.Form["action"];
            
            if(action == "generatekeypair")
            {
                // Generate a new P-256 key pair
                KeyPair generatedKey = KeyPair.Generate(KeyTypes.P256);
                Pds.PdsDb.SetConfigProperty("UserPublicKeyMultibase", generatedKey.PublicKeyMultibase);
                Pds.PdsDb.SetConfigProperty("UserPrivateKeyMultibase", generatedKey.PrivateKeyMultibase);
            }
            else if(action == "generateuserpassword")
            {
                // Generate a new user password
                string newPassword = PasswordHasher.CreateNewAdminPassword();
                string hashedPassword = PasswordHasher.HashPassword(newPassword);
                Pds.PdsDb.SetConfigProperty("UserHashedPassword", hashedPassword);
                
                // Set a short-lived cookie with the cleartext password to display after redirect
                HttpContext.Response.Cookies.Append("generated_user_password", newPassword, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    MaxAge = TimeSpan.FromMinutes(1)
                });
            }
            else if(action == "generateadminpassword")
            {
                // Generate a new admin password
                string newPassword = PasswordHasher.CreateNewAdminPassword();
                string hashedPassword = PasswordHasher.HashPassword(newPassword);
                Pds.PdsDb.SetConfigProperty("AdminHashedPassword", hashedPassword);
                
                // Set a short-lived cookie with the cleartext password to display after redirect
                HttpContext.Response.Cookies.Append("generated_admin_password", newPassword, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    MaxAge = TimeSpan.FromMinutes(1)
                });
            }
            else if(action == "installuserrepo")
            {
                // Validate confirmation text
                string? confirmText = HttpContext.Request.Form["confirm_text"];
                if(string.IsNullOrEmpty(confirmText) || !confirmText.Equals("agree", StringComparison.OrdinalIgnoreCase))
                {
                    HttpContext.Response.Cookies.Append("install_repo_error", "You must type 'agree' to confirm this action.", new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        MaxAge = TimeSpan.FromMinutes(1)
                    });
                    
                    HttpContext.Response.Redirect("/admin/actions");
                    return Results.Empty;
                }
                
                // Install user repo using keypair from config
                bool hasPrivateKey = Pds.PdsDb.ConfigPropertyExists("UserPrivateKeyMultibase");
                bool hasPublicKey = Pds.PdsDb.ConfigPropertyExists("UserPublicKeyMultibase");
                
                if(!hasPrivateKey || !hasPublicKey)
                {
                    // Set error cookie to show after redirect
                    HttpContext.Response.Cookies.Append("install_repo_error", "User key pair not configured. Please generate a key pair first.", new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        MaxAge = TimeSpan.FromMinutes(1)
                    });
                }
                else
                {
                    var signingFunc = auth.Signer.CreateCommitSigningFunction(
                        Pds.PdsDb.GetConfigProperty("UserPrivateKeyMultibase"),
                        Pds.PdsDb.GetConfigProperty("UserPublicKeyMultibase"));
                    Installer.InstallRepo(Pds.LocalFileSystem, Pds.Logger, signingFunc);
                    
                    // Set success cookie to show after redirect
                    HttpContext.Response.Cookies.Append("install_repo_success", "User repo installed successfully.", new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        MaxAge = TimeSpan.FromMinutes(1)
                    });
                }
            }
            else if(action == "activateaccount")
            {
                Pds.ActivateAccount();
            }
            
            // POST-Redirect-GET pattern to prevent form resubmission
            HttpContext.Response.Redirect("/admin/actions");
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
        // Check for generated password cookie and clear it
        //
        string? generatedPassword = null;
        if(HttpContext.Request.Cookies.TryGetValue("generated_user_password", out generatedPassword))
        {
            // Delete the cookie immediately after reading
            HttpContext.Response.Cookies.Delete("generated_user_password");
        }

        string? generatedAdminPassword = null;
        if(HttpContext.Request.Cookies.TryGetValue("generated_admin_password", out generatedAdminPassword))
        {
            // Delete the cookie immediately after reading
            HttpContext.Response.Cookies.Delete("generated_admin_password");
        }

        string? installRepoError = null;
        if(HttpContext.Request.Cookies.TryGetValue("install_repo_error", out installRepoError))
        {
            // Delete the cookie immediately after reading
            HttpContext.Response.Cookies.Delete("install_repo_error");
        }

        string? installRepoSuccess = null;
        if(HttpContext.Request.Cookies.TryGetValue("install_repo_success", out installRepoSuccess))
        {
            // Delete the cookie immediately after reading
            HttpContext.Response.Cookies.Delete("install_repo_success");
        }


        //
        // Get current public key value
        //
        string HtmlEncode(string value) => WebUtility.HtmlEncode(value);
        string userPublicKeyValue = Pds.PdsDb.ConfigPropertyExists("UserPublicKeyMultibase") 
            ? HtmlEncode(Pds.PdsDb.GetConfigProperty("UserPublicKeyMultibase")) 
            : "<span class=\"dimmed\">empty</span>";
        
        string userPasswordStatus = Pds.PdsDb.ConfigPropertyExists("UserHashedPassword")
            ? "<span style=\"color: #4caf50;\">configured</span>"
            : "<span class=\"dimmed\">not configured</span>";
        
        string adminPasswordStatus = Pds.PdsDb.ConfigPropertyExists("AdminHashedPassword")
            ? "<span style=\"color: #4caf50;\">configured</span>"
            : "<span class=\"dimmed\">not configured</span>";


        //
        // return actions page
        //
        string html = $@"
        <html>
        <head>
        <title>Admin - Actions - {TryGetPdsHostname()}</title>
        <style>
            body {{ background-color: #16181c; color: #e7e9ea; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; padding: 40px 20px; }}
            .container {{ max-width: 800px; margin: 0 0 0 40px; }}
            h1 {{ color: #8899a6; margin-bottom: 24px; }}
            h2 {{ color: #8899a6; margin-top: 32px; margin-bottom: 16px; font-size: 18px; }}
            .info-card {{ background-color: #2f3336; border-radius: 8px; padding: 12px 16px; margin-bottom: 8px; }}
            .label {{ color: #8899a6; font-size: 14px; }}
            .value {{ color: #1d9bf0; font-size: 14px; word-break: break-all; }}
            {GetNavbarCss()}
            .action-btn {{ background-color: #4caf50; color: white; border: none; padding: 6px 12px; border-radius: 5px; cursor: pointer; font-size: 13px; font-weight: 500; font-family: inherit; }}
            .action-btn:hover {{ background-color: #388e3c; }}
            .action-btn-destructive {{ background-color: #f44336; color: white; border: none; padding: 6px 12px; border-radius: 5px; cursor: pointer; font-size: 13px; font-weight: 500; font-family: inherit; }}
            .action-btn-destructive:hover {{ background-color: #d32f2f; }}
            .dimmed {{ color: #657786; }}
            .password-display {{ background-color: #1a2634; border: 2px solid #1d9bf0; border-radius: 8px; padding: 16px; margin-bottom: 16px; }}
            .password-display .label {{ color: #1d9bf0; font-weight: 500; margin-bottom: 8px; }}
            .password-display .value {{ font-family: monospace; font-size: 14px; color: #e7e9ea; word-break: break-all; user-select: all; }}
            .password-warning {{ color: #f0a81d; font-size: 13px; margin-top: 8px; }}
            .error-display {{ background-color: #2a1a1a; border: 2px solid #f44336; border-radius: 8px; padding: 16px; margin-bottom: 16px; }}
            .error-display .label {{ color: #f44336; font-weight: 500; margin-bottom: 8px; }}
            .error-display .value {{ font-size: 14px; color: #e7e9ea; }}
            .success-display {{ background-color: #1a2a1a; border: 2px solid #4caf50; border-radius: 8px; padding: 16px; margin-bottom: 16px; }}
            .success-display .label {{ color: #4caf50; font-weight: 500; margin-bottom: 8px; }}
            .success-display .value {{ font-size: 14px; color: #e7e9ea; }}
        </style>
        </head>
        <body>
        <div class=""container"">
        {GetNavbarHtml("actions")}
        <h1>Actions</h1>

        <h2>User Key Pair</h2>
        <div class=""info-card"">
            <div class=""label"">UserPublicKeyMultibase</div>
            <div class=""value"">{userPublicKeyValue}</div>
        </div>
        <form method=""post"" action=""/admin/actions"" style=""margin-top: 16px;"" onsubmit=""return confirm('Are you sure you want to generate a new key pair? This will overwrite the existing keys.');"">
            <input type=""hidden"" name=""csrf_token"" value=""{csrfToken}"" />
            <input type=""hidden"" name=""action"" value=""generatekeypair"" />
            <button type=""submit"" class=""action-btn-destructive"">Generate Key Pair</button>
        </form>

        <h2>Install User Repo</h2>
        {(installRepoError != null ? $@"
        <div class=""error-display"">
            <div class=""label"">Error</div>
            <div class=""value"">{HtmlEncode(installRepoError)}</div>
        </div>
        " : "")}
        {(installRepoSuccess != null ? $@"
        <div class=""success-display"">
            <div class=""label"">Success</div>
            <div class=""value"">{HtmlEncode(installRepoSuccess)}</div>
        </div>
        " : "")}
        <div class=""info-card"">
            <div class=""label"">Install a fresh user repository using the configured key pair</div>
        </div>
        <form method=""post"" action=""/admin/actions"" style=""margin-top: 16px;"">
            <input type=""hidden"" name=""csrf_token"" value=""{csrfToken}"" />
            <input type=""hidden"" name=""action"" value=""installuserrepo"" />
            <div style=""margin-bottom: 12px;"">
                <label for=""confirm_text"" style=""color: #f0a81d; font-size: 14px;"">Type &quot;agree&quot; to confirm (this will delete any existing repo data):</label>
                <input type=""text"" id=""confirm_text"" name=""confirm_text"" autocomplete=""off"" style=""display: block; margin-top: 8px; padding: 8px 12px; border-radius: 4px; border: 1px solid #2f3336; background-color: #16181c; color: #e7e9ea; font-size: 14px; width: 200px;"" />
            </div>
            <button type=""submit"" class=""action-btn-destructive"">Install User Repo</button>
        </form>

        <h2>User Password</h2>
        {(generatedPassword != null ? $@"
        <div class=""password-display"">
            <div class=""label"">New Password Generated - Copy Now!</div>
            <div class=""value"">{HtmlEncode(generatedPassword)}</div>
            <div class=""password-warning"">This password will not be shown again. Copy it now and store it securely.</div>
        </div>
        " : "")}
        <div class=""info-card"">
            <div class=""label"">UserHashedPassword</div>
            <div class=""value"">{userPasswordStatus}</div>
        </div>
        <form method=""post"" action=""/admin/actions"" style=""margin-top: 16px;"" onsubmit=""return confirm('Are you sure you want to generate a new password? This will invalidate the existing password.');"">
            <input type=""hidden"" name=""csrf_token"" value=""{csrfToken}"" />
            <input type=""hidden"" name=""action"" value=""generateuserpassword"" />
            <button type=""submit"" class=""action-btn-destructive"">Generate User Password</button>
        </form>

        <h2>Activate Account</h2>
        <div class=""info-card"">
            <div class=""label"">Activate account. This sets the config property UserIsActive, and generates firehose events for #identity and #account.</div>
        </div>
        <form method=""post"" action=""/admin/actions"" style=""margin-top: 16px;"" onsubmit=""return confirm('Are you sure you want to activate the account?');"">
            <input type=""hidden"" name=""csrf_token"" value=""{csrfToken}"" />
            <input type=""hidden"" name=""action"" value=""activateaccount"" />
            <button type=""submit"" class=""action-btn-destructive"">Activate Account</button>
        </form>

        <h2>Admin Password</h2>
        {(generatedAdminPassword != null ? $@"
        <div class=""password-display"">
            <div class=""label"">New Password Generated - Copy Now!</div>
            <div class=""value"">{HtmlEncode(generatedAdminPassword)}</div>
            <div class=""password-warning"">This password will not be shown again. Copy it now and store it securely.</div>
        </div>
        " : "")}
        <div class=""info-card"">
            <div class=""label"">AdminHashedPassword</div>
            <div class=""value"">{adminPasswordStatus}</div>
        </div>
        <form method=""post"" action=""/admin/actions"" style=""margin-top: 16px;"" onsubmit=""return confirm('Are you sure you want to generate a new admin password? This will invalidate the existing admin password.');"">
            <input type=""hidden"" name=""csrf_token"" value=""{csrfToken}"" />
            <input type=""hidden"" name=""action"" value=""generateadminpassword"" />
            <button type=""submit"" class=""action-btn-destructive"">Generate Admin Password</button>
        </form>

        </div>
        </body>
        </html>
        ";
        return Results.Content(html, "text/html", statusCode: 200);
    }
}
