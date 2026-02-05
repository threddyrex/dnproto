
using Microsoft.AspNetCore.Http;
using dnproto.log;

namespace dnproto.pds;


public class Statistics
{
    

    public static void IncrementStatistics_Connect(HttpContext ctx, PdsDb db, IDnProtoLogger logger)
    {
        try
        {
            //
            // Get caller info
            //
            (string ipAddress, string userAgent) = GetCallerInfo(ctx);


            //
            // Log connection
            //
            string path = ctx.Request.Path;
            logger.LogInfo($"[CONNECT]  {ipAddress}  {path}  {userAgent}");


            //
            // uptimerobot sends from all over, so just group them all together
            //
            if(userAgent!.Contains("www.uptimerobot.com"))
            {
                ipAddress = "global";
            }


            db.IncrementStatistic(new StatisticKey { Name = "Connect", IpAddress = ipAddress!, UserAgent = userAgent! });
        }
        catch
        {
            // don't throw on this
        }
    }

    public static void IncrementStatistics_ApplyWrites(HttpContext ctx, PdsDb db, IDnProtoLogger logger)
    {
        try
        {
            //
            // Get caller info
            //
            (string ipAddress, string userAgent) = GetCallerInfo(ctx);


            //
            // Increment
            //
            db.IncrementStatistic(new StatisticKey { Name = "ApplyWrites", IpAddress = ipAddress!, UserAgent = userAgent! });
        }
        catch
        {
            // don't throw on this
        }
    }    



    protected static (string ipAddress, string userAgent) GetCallerInfo(HttpContext ctx)
    {
        string? userAgent = ctx.Request.Headers.ContainsKey("User-Agent") ? ctx.Request.Headers["User-Agent"].ToString() : null;
        string? ipAddress = ctx.Request.Headers.ContainsKey("X-Forwarded-For") ? ctx.Request.Headers["X-Forwarded-For"].ToString() : null;

        if(string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = ctx.Connection.RemoteIpAddress?.ToString();
        }

        if(string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = "unknown";
        }

        if(string.IsNullOrEmpty(userAgent))
        {
            userAgent = "unknown";
        }

        return (ipAddress, userAgent);
    }



}