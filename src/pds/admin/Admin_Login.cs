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
                button { background-color: #1d9bf0; color: white; border: none; padding: 12px 24px; border-radius: 6px; font-size: 16px; font-weight: bold; cursor: pointer; }
                button:hover { background-color: #1a8cd8; }
            </style>
            </head>
            <body>
            <div class=""container"">
            <h1>Login Required</h1>
            <p>You must be logged in to access account information.</p>
            <form method=""post"" action=""/admin/login"">
                <label for=""username"">Username</label>
                <input type=""text"" id=""username"" name=""username"" />
                <label for=""password"">Password</label>
                <input type=""password"" id=""password"" name=""password"" />
                <button type=""submit"">Login</button>
            </form>
            </div>
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


            ActorInfo? actorInfo = BlueskyClient.ResolveActorInfo(userName!);
            bool actorExists = actorInfo != null;
            string? storedHashedPassword = Pds.Config.UserHashedPassword;
            bool passwordMatches = PasswordHasher.VerifyPassword(storedHashedPassword, password);
            bool authSucceeded = actorExists && passwordMatches;


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
                    UserAgent = GetCallerUserAgent() ?? "unknown"
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