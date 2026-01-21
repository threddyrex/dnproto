using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class OAuth_ProtectedResource : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        return Results.Json(new 
        {
            resource = $"https://{Pds.Config.PdsHostname}",
            authorization_servers = new JsonArray() {$"https://{Pds.Config.PdsHostname}"},
            scopes_supported = new JsonArray(){},
            bearer_methods_supported = new JsonArray() {"header"},
            resource_documentation = "https://atproto.com"
        },
        statusCode: 200);
    }
}