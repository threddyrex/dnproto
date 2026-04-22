
using dnproto.log;
using Microsoft.AspNetCore.Http;


namespace dnproto.pds.admin;


public class BaseAdmin
{
    public required Pds Pds;

    public required HttpContext HttpContext;


    protected bool AdminInterfaceIsEnabled()
    {
        return Pds.PdsDb.GetConfigPropertyBool("FeatureEnabled_AdminDashboard");
    }

    protected bool PasskeysEnabled()
    {
        return Pds.PdsDb.GetConfigPropertyBool("FeatureEnabled_Passkeys");
    }

    protected AdminSession? GetValidAdminSession()
    {
        string? sessionId;
        HttpContext.Request.Cookies.TryGetValue("adminSessionId", out sessionId);

        if(string.IsNullOrEmpty(sessionId))
        {
            return null;
        }

        AdminSession? adminSession = Pds.PdsDb.GetValidAdminSession(sessionId!, GetCallerIpAddress());
        return adminSession;
    }


    protected bool AdminIsAuthenticated()
    {
        AdminSession? adminSession = GetValidAdminSession();
        return adminSession != null;
    }



    protected string GetCallerIpAddress()
    {
        //
        // Get IP address from caddy
        //
        string? forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if(!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor;
        }

        //
        // Fallback for development without a reverse proxy
        //
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        
        if(string.IsNullOrEmpty(remoteIp))
        {
            throw new Exception("Unable to determine caller IP address");
        }

        return remoteIp;
    }


    protected string? GetCallerUserAgent()
    {
        try
        {
            string? userAgent = HttpContext.Request.Headers.ContainsKey("User-Agent") ? HttpContext.Request.Headers["User-Agent"].ToString() : null;
            return userAgent;
        }
        catch
        {
            return null;
        }
    }

    protected string TryGetPdsHostname()
    {
        if(Pds.PdsDb.ConfigPropertyExists("PdsHostname"))
        {
            return Pds.PdsDb.GetConfigProperty("PdsHostname")!;
        }
        else
        {
            return "(PdsHostname not set)";
        }
    }


    protected void IncrementStatistics()
    {
        Statistics.IncrementStatistics_Connect(HttpContext, Pds.PdsDb, Pds.Logger);
    }

    protected string GetNavbarCss()
    {
        return @"
            .navbar { position: fixed; top: 0; left: 0; bottom: 0; width: 180px; display: flex; flex-direction: column; align-items: stretch; gap: 8px; padding: 24px 16px; border-right: 1px solid #2f3336; background-color: #1a1c20; box-sizing: border-box; overflow-y: auto; }
            .nav-btn { background-color: #4caf50; color: white; border: none; padding: 8px 12px; border-radius: 5px; cursor: pointer; font-size: 13px; font-weight: 500; text-decoration: none; display: block; text-align: left; }
            .nav-btn:hover { background-color: #388e3c; }
            .nav-btn.active { background-color: #388e3c; }
            .nav-btn-destructive { background-color: #f44336; color: white; border: none; padding: 8px 12px; border-radius: 5px; cursor: pointer; font-size: 13px; font-weight: 500; text-decoration: none; display: block; text-align: left; }
            .nav-btn-destructive:hover { background-color: #d32f2f; }
            .nav-btn-destructive.active { background-color: #d32f2f; }
            .nav-spacer { flex-grow: 1; }
            .logout-btn { background-color: #1d9bf0; color: white; border: none; padding: 8px 12px; border-radius: 5px; cursor: pointer; font-size: 13px; font-weight: 500; font-family: inherit; width: 100%; text-align: left; }
            .logout-btn:hover { background-color: #1a8cd8; }
            @media (max-width: 700px) { .navbar { position: static; width: auto; flex-direction: row; flex-wrap: wrap; border-right: none; border-bottom: 1px solid #2f3336; margin-bottom: 24px; } .nav-btn, .nav-btn-destructive, .logout-btn { display: inline-block; width: auto; text-align: center; } }
        ";
    }

    protected string GetNavbarHtml(string activePage)
    {
        string ActiveClass(string page) => activePage == page ? " active" : "";
        
        return $@"
        <div class=""navbar"">
            <a href=""/admin/"" class=""nav-btn{ActiveClass("home")}"">Home</a>
            <a href=""/admin/sessions"" class=""nav-btn{ActiveClass("sessions")}"">Sessions</a>
            <a href=""/admin/stats"" class=""nav-btn{ActiveClass("stats")}"">Statistics</a>
            <div class=""nav-spacer""></div>
            <a href=""/admin/config"" class=""nav-btn-destructive{ActiveClass("config")}"">Config</a>
            <a href=""/admin/actions"" class=""nav-btn-destructive{ActiveClass("actions")}"">Actions</a>
            <a href=""/admin/passkeys"" class=""nav-btn-destructive{ActiveClass("passkeys")}"">Passkeys</a>
            <form method=""post"" action=""/admin/logout"" style=""margin: 0;"">
                <button type=""submit"" class=""logout-btn"">Log out</button>
            </form>
        </div>";
    }

}