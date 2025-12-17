using Microsoft.AspNetCore.Builder;

using dnproto.log;

namespace dnproto.pds.xrpc;

public class XrpcEndpoints
{
    public static void MapEndpoints(WebApplication app, BaseLogger logger, PdsConfig pdsConfig)
    {
        app.MapGet("/hello", () => new Hello(){Logger = logger, PdsConfig = pdsConfig}.GetResponse());
        app.MapGet("/xrpc/_health", () => new Health(){Logger = logger, PdsConfig = pdsConfig}.GetResponse());        
    }
    
}