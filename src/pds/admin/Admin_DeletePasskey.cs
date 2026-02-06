using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// </summary>
public class Admin_DeletePasskey : BaseAdmin
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
        // We got this far, so delete the passkey
        //
        string? name = HttpContext.Request.Form["name"];
        if(string.IsNullOrEmpty(name) == false)
        {
            Pds.PdsDb.DeletePasskeyByName(name);
        }




        //
        // Redirect to passkeys page
        //
        HttpContext.Response.Redirect("/admin/passkeys");
        return Results.Empty;
    }





}