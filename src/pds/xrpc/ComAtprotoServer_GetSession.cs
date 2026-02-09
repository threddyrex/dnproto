using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoServer_GetSession : BaseXrpcCommand
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
        // Return session info
        //
        return Results.Json(new 
        {
            did = Pds.PdsDb.GetConfigProperty("UserDid"),
            handle = Pds.PdsDb.GetConfigProperty("UserHandle"),
            email = Pds.PdsDb.GetConfigProperty("UserEmail"),
            emailConfirmed = true
        },
        statusCode: 200);
    }
}