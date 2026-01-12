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
        //
        // Check auth
        //
        if(CheckUserAuth() == false)
        {
            Pds.Logger.LogInfo("ComAtprotoServer_DeactivateAccount: Unauthorized call to DeactivateAccount.");
            return Results.Json(new { error = "InvalidRequest", message = "Need auth" }, statusCode: 204);
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
