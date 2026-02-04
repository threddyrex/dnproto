using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// </summary>
public class Admin_DeleteAdminSession : BaseAdmin
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
        // We got this far, so delete the admin session
        //
        string? sessionId = HttpContext.Request.Form["sessionId"];
        if(string.IsNullOrEmpty(sessionId) == false)
        {
            Pds.PdsDb.DeleteAdminSession(sessionId);
        }




        //
        // Redirect to sessions page
        //
        HttpContext.Response.Redirect("/admin/sessions");
        return Results.Empty;
    }





}