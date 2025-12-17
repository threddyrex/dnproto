
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using dnproto.log;

namespace dnproto.pds;

public class Xrpc
{
        
    public static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/hello", () => Xrpc_Hello.GetResponse());
        app.MapGet("/xrpc/_health", () => Xrpc_Health.GetResponse());        
    }
}