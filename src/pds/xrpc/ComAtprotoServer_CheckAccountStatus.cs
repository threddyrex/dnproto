using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;

/// <summary>
/// https://docs.bsky.app/docs/api/com-atproto-server-check-account-status
/// </summary>
public class ComAtprotoServer_CheckAccountStatus : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        IncrementStatistics();
        
        //
        // Require auth
        //
        if(UserIsAuthenticated() == false)
        {
            var (response, statusCode) = GetAuthenticationFailureResponse();
            return Results.Json(response, statusCode: statusCode);
        }



        //
        // Return status. There are more required items we need to provide,
        // but just start with these for now.
        //
        var repoCommit = Pds.PdsDb.GetRepoCommit();
        return Results.Json(new 
        {
            activated = Pds.PdsDb.GetConfigPropertyBool("UserIsActive"),
            validDid = true,
            repoCommit = repoCommit.Cid?.Base32,
            repoRev = repoCommit.Rev
        },
        statusCode: 200);
    }
}