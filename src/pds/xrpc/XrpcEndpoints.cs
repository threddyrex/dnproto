using Microsoft.AspNetCore.Builder;

using dnproto.sdk.log;

namespace dnproto.pds.xrpc;

public class XrpcEndpoints
{
    public static void MapEndpoints(WebApplication app, BaseLogger logger, PdsConfig pdsConfig)
    {
        app.MapGet("/hello", () => new Hello(){Logger = logger, PdsConfig = pdsConfig}.GetResponse());
        app.MapGet("/xrpc/_health", () => new Health(){Logger = logger, PdsConfig = pdsConfig}.GetResponse());
        app.MapGet("/xrpc/com.atproto.server.describeServer", () => new ComAtprotoServer_DescribeServer(){Logger = logger, PdsConfig = pdsConfig}.GetResponse());

        logger.LogInfo("");
        logger.LogInfo("Mapped XRPC endpoints:");
        logger.LogInfo("");
        logger.LogInfo($"https://{pdsConfig.Host}:{pdsConfig.Port}/xrpc/_health");
        logger.LogInfo($"https://{pdsConfig.Host}:{pdsConfig.Port}/xrpc/com.atproto.server.describeServer");
        logger.LogInfo("");

    }
    
}