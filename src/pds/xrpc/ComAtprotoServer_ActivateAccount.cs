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
        // Check auth
        //
        if(CheckUserAuth() == false)
        {
            Pds.Logger.LogInfo("ComAtprotoServer_ActivateAccount: Unauthorized call to ActivateAccount.");
            return Results.Json(new { error = "InvalidRequest", message = "Need auth" }, statusCode: 204);
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
