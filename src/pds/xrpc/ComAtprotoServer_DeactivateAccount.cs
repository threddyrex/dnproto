using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoServer_DeactivateAccount : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        IncrementStatistics();
        
        //
        // Require auth
        //
        if(UserIsAuthenticated(allowedAuthTypes: [AuthType.Legacy]) == false)
        {
            var (response, statusCode) = GetAuthenticationFailureResponse();
            return Results.Json(response, statusCode: statusCode);
        }


        //
        // Deactivate account
        //
        Pds.DeactivateAccount();

        //
        // Return OK (no body)
        //
        return Results.Ok();
    }
}
