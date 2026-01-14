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
        //
        // Require auth
        //
        if(UserIsFullyAuthorized() == false)
        {
            var (response, statusCode) = GetAuthFailureResponse();
            return Results.Json(response, statusCode: statusCode);
        }



        //
        // Return session info
        //
        return Results.Json(new 
        {
            did = Pds.Config.UserDid,
            handle = Pds.Config.UserHandle,
            email = Pds.Config.UserEmail,
            emailConfirmed = true
        },
        statusCode: 200);
    }
}