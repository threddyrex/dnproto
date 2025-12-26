using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoServer_GetSession : BaseXrpcCommand
{
    public override IResult GetResponse()
    {

        //
        // Get the jwt from the caller's Authorization header
        //
        string? accessJwt = GetAccessJwt();
        ClaimsPrincipal? claimsPrincipal = JwtSecret.VerifyAccessJwt(accessJwt, Pds.Config.JwtSecret);

        string? userDid = JwtSecret.GetDidFromClaimsPrincipal(claimsPrincipal);
        bool didMatches = userDid == Pds.Config.UserDid;
        string? handle = null;

        if(didMatches)
        {
            handle = Pds.Config.UserHandle;
        }

        //
        // Return session info
        //
        return Results.Json(new 
        {
            did = userDid,
            handle = handle
        },
        statusCode: 200);
    }
}