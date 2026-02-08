using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// Deletes statistics older than 24 hours
/// </summary>
public class Admin_DeleteOldStatistics : BaseAdmin
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
        // We got this far, so delete old statistics
        //
        Pds.PdsDb.DeleteOldStatistics();




        //
        // Redirect to stats page
        //
        HttpContext.Response.Redirect("/admin/stats");
        return Results.Empty;
    }
}
