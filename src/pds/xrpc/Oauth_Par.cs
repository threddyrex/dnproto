using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class Oauth_Par : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        LogRequest(HttpContext);
        
        return Results.Json(new
        {
            keys = new JsonArray(){},
        },
        statusCode: 401);
    }
}