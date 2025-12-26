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
        // Get the jwt from the caller's Authorization header
        //
        string? originalRefreshJwt = GetAccessJwt();
        ClaimsPrincipal? claimsPrincipal = JwtSecret.VerifyRefreshJwt(originalRefreshJwt, Pds.Config.JwtSecret);
        bool claimsPrincipalExists = claimsPrincipal != null;
        string? userDid = JwtSecret.GetDidFromClaimsPrincipal(claimsPrincipal);
        bool didMatches = userDid == Pds.Config.UserDid;
        string? handle = null;

        if(didMatches)
        {
            handle = Pds.Config.UserHandle;
        }

        string? accessJwt = null;
        string? newRefreshJwt = null;

        if(claimsPrincipalExists && didMatches)
        {
            accessJwt = JwtSecret.GenerateAccessJwt(userDid, Pds.Config.PdsDid, Pds.Config.JwtSecret);
            newRefreshJwt = JwtSecret.GenerateRefreshJwt(userDid, Pds.Config.PdsDid, Pds.Config.JwtSecret);
        }

        //
        // Return session info
        //
        return Results.Json(new 
        {
            did = userDid,
            handle = handle,
            accessJwt = accessJwt,
            refreshJwt = newRefreshJwt,
            claimsPrincipalExists = claimsPrincipalExists,
            didMatches = didMatches
        },
        statusCode: 200);
    }
}