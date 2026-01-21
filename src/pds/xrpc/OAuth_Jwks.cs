using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class OAuth_Jwks : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        return Results.Json(new
        {
            keys = new JsonArray(){},
        },
        statusCode: 200);
    }
}