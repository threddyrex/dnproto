using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.admin;

/// <summary>
/// </summary>
public class Admin_DeleteStatistic : BaseAdmin
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
        // We got this far, so delete the statistic
        //
        string? name = HttpContext.Request.Form["name"];
        string? ipAddress = HttpContext.Request.Form["ipAddress"];
        string? userAgent = HttpContext.Request.Form["userAgent"];
        if(string.IsNullOrEmpty(name) == false && string.IsNullOrEmpty(ipAddress) == false && string.IsNullOrEmpty(userAgent) == false)
        {
            Pds.PdsDb.DeleteStatisticByKey(new StatisticKey { Name = name, IpAddress = ipAddress, UserAgent = userAgent });
        }




        //
        // Redirect to stats page
        //
        HttpContext.Response.Redirect("/admin/stats");
        return Results.Empty;
    }





}
