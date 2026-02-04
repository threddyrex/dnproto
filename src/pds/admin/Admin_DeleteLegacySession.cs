using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// </summary>
public class Admin_DeleteLegacySession : BaseAdmin
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
        // We got this far, so delete the legacy session
        //
        string? refreshJwt = HttpContext.Request.Form["refreshJwt"];
        if(string.IsNullOrEmpty(refreshJwt) == false)
        {
            Pds.PdsDb.DeleteLegacySessionForRefreshJwt(refreshJwt);
        }




        //
        // Redirect to home
        //
        HttpContext.Response.Redirect("/admin/");
        return Results.Empty;
    }





}