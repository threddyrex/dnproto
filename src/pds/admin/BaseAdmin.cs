
using Microsoft.AspNetCore.Http;


namespace dnproto.pds.admin;


public class BaseAdmin
{
    public required Pds Pds;

    public required HttpContext HttpContext;


    protected bool AdminInterfaceIsEnabled()
    {
        return Pds.PdsDb.GetConfig().AdminInterfaceIsEnabled;
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
        // If we're not in dev, don't allow this
        //
        if(string.Equals(Pds.Config.PdsHostname, "localhost", StringComparison.OrdinalIgnoreCase) == false)
        {
            throw new Exception("Unable to determine caller IP address");
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

    protected void IncrementStatistics()
    {
        try
        {
            string? userAgent = HttpContext.Request.Headers.ContainsKey("User-Agent") ? HttpContext.Request.Headers["User-Agent"].ToString() : null;
            string? ipAddress = HttpContext.Request.Headers.ContainsKey("X-Forwarded-For") ? HttpContext.Request.Headers["X-Forwarded-For"].ToString() : null;

            if(string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            }


            Pds.PdsDb.IncrementStatistic("Connection Count", $"{ipAddress!} ({userAgent})");
            Pds.PdsDb.IncrementStatistic("Connection Count (Admin)", $"{ipAddress!} ({userAgent})");
        }
        catch
        {
            // don't throw on this
        }
    }
}