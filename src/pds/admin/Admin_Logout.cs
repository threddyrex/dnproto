using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// </summary>
public class Admin_Logout : BaseAdmin
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
        // We got this far, so log out
        //
        if(adminSession != null)
        {
            Pds.PdsDb.DeleteAdminSession(adminSession.SessionId);
        }



        //
        // Clear cookie
        //
        HttpContext.Response.Cookies.Append("adminSessionId", "", new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/admin"
        });



        //
        // Redirect to login
        //
        HttpContext.Response.Redirect("/admin/login");
        return Results.Empty;
    }





}