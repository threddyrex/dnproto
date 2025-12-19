using System.Text.Json.Nodes;
using dnproto.sdk.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoServer_CreateAccount : BaseXrpcCommand
{
    public override IResult GetResponse()
    {

        //
        // Get body input
        //
        JsonNode? requestBody = GetRequestBodyAsJson();
        string? handle, did, inviteCode, password;

        if(!CheckRequestBodyParam(requestBody, "handle", out handle)
            || ! CheckRequestBodyParam(requestBody, "did", out did)
            || ! CheckRequestBodyParam(requestBody, "inviteCode", out inviteCode)
            || ! CheckRequestBodyParam(requestBody, "password", out password)
            || string.IsNullOrEmpty(inviteCode)
            || string.IsNullOrEmpty(handle)
            || string.IsNullOrEmpty(did)
        )
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: invalid params." }, statusCode: 400);
        }

        //
        // See if we have a valid invite code
        //
        int inviteCodeUseCount = Pds.PdsDb.GetInviteCodeCount(inviteCode);


        //
        // See if account exists
        //
        bool accountExists = Pds.PdsDb.AccountExists(handle, did);


        //
        // Return info
        //
        return Results.Json(new { inviteCodeUseCount = inviteCodeUseCount, accountExists = accountExists }, statusCode: 200);
    }
}