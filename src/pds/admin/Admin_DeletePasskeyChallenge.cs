using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// </summary>
public class Admin_DeletePasskeyChallenge : BaseAdmin
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



        //
        // We got this far, so delete the passkey challenge
        //
        string? challenge = HttpContext.Request.Form["challenge"];
        if(string.IsNullOrEmpty(challenge) == false)
        {
            Pds.PdsDb.DeletePasskeyChallenge(challenge);
        }




        //
        // Redirect to home
        //
        HttpContext.Response.Redirect("/admin/");
        return Results.Empty;
    }





}