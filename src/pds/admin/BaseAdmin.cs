
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

}