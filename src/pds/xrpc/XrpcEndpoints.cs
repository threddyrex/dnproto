using Microsoft.AspNetCore.Builder;

using dnproto.sdk.log;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;

public class XrpcEndpoints
{
    public static void MapEndpoints(WebApplication app, BaseLogger logger, PdsConfig pdsConfig)
    {
        app.MapGet("/hello", (HttpContext context) => new Hello(){Logger = logger, PdsConfig = pdsConfig, HttpContext = context}.GetResponse());
        app.MapGet("/xrpc/_health", (HttpContext context) => new Health(){Logger = logger, PdsConfig = pdsConfig, HttpContext = context}.GetResponse());
        app.MapGet("/xrpc/com.atproto.server.describeServer", (HttpContext context) => new ComAtprotoServer_DescribeServer(){Logger = logger, PdsConfig = pdsConfig, HttpContext = context}.GetResponse());
        app.MapGet("/xrpc/com.atproto.identity.resolveHandle", (HttpContext context) => new ComAtprotoIdentity_ResolveHandle(){Logger = logger, PdsConfig = pdsConfig, HttpContext = context}.GetResponse());

        logger.LogInfo("");
        logger.LogInfo("Mapped XRPC endpoints:");
        logger.LogInfo("");
        logger.LogInfo($"https://{pdsConfig.Host}:{pdsConfig.Port}/hello");
        logger.LogInfo($"https://{pdsConfig.Host}:{pdsConfig.Port}/xrpc/_health");
        logger.LogInfo($"https://{pdsConfig.Host}:{pdsConfig.Port}/xrpc/com.atproto.server.describeServer");
        logger.LogInfo($"https://{pdsConfig.Host}:{pdsConfig.Port}/xrpc/com.atproto.identity.resolveHandle");
        logger.LogInfo("");

    }
    
}