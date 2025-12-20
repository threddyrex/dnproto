using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.sdk.auth;
using dnproto.sdk.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoServer_RefreshSession : BaseXrpcCommand
{
    public override IResult GetResponse()
    {

        //
        // Get the jwt from the caller's Authorization header
        //
        string? refreshJwt = GetAccessJwt();
        bool claimsPrincipalExists = false;

        if(!string.IsNullOrEmpty(refreshJwt))
        {
            ClaimsPrincipal? claimsPrincipal = JwtSecret.VerifyRefreshJwt(refreshJwt, Pds.PdsConfig.JwtSecret);
            claimsPrincipalExists = claimsPrincipal != null;
        }

        //
        // Return session info
        //
        return Results.Json(new 
        {
            inputRefreshJwt = refreshJwt,
            claimsPrincipalExists = claimsPrincipalExists
        },
        statusCode: 200);
    }
}