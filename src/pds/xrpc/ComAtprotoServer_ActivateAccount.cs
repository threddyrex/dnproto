using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoServer_ActivateAccount : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        //
        // Require auth
        //
        if(UserIsFullyAuthorized() == false)
        {
            var (response, statusCode) = GetAuthFailureResponse();
            return Results.Json(response, statusCode: statusCode);
        }



        //
        // Activate account
        //
        Pds.ActivateAccount();


        //
        // Return OK (no body)
        //
        return Results.Ok();
    }
}
