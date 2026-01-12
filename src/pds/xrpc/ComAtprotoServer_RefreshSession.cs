using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoServer_RefreshSession : BaseXrpcCommand
{
    public IResult GetResponse()
    {

        //
        // Get the refresh jwt from the caller's Authorization header
        //
        string? originalRefreshJwt = GetAccessJwt();
        ClaimsPrincipal? claimsPrincipal = JwtSecret.VerifyRefreshJwt(originalRefreshJwt, Pds.Config.JwtSecret);
        
        if (claimsPrincipal == null)
        {
            return Results.Json(new 
            {
                error = "ExpiredToken",
                message = "Token has expired"
            },
            statusCode: 400);
        }

        string? userDid = JwtSecret.GetDidFromClaimsPrincipal(claimsPrincipal);
        
        if (userDid != Pds.Config.UserDid)
        {
            return Results.Json(new 
            {
                error = "InvalidToken",
                message = "Token did not match expected user"
            },
            statusCode: 401);
        }

        string? handle = Pds.Config.UserHandle;
        string? accessJwt = JwtSecret.GenerateAccessJwt(userDid, Pds.Config.PdsDid, Pds.Config.JwtSecret);
        string? newRefreshJwt = JwtSecret.GenerateRefreshJwt(userDid, Pds.Config.PdsDid, Pds.Config.JwtSecret);

        //
        // Return session info
        //
        return Results.Json(new 
        {
            did = userDid,
            handle = handle,
            accessJwt = accessJwt,
            refreshJwt = newRefreshJwt
        },
        statusCode: 200);
    }
}