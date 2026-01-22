using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class Oauth_Jwks : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        if(!Pds.Config.OauthIsEnabled)
        {
            return Results.Json(new{}, statusCode: 403);
        }

        return Results.Json(new
        {
            keys = new JsonArray(){},
        },
        statusCode: 200);
    }
}