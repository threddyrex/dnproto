using System.Text.Json.Nodes;
using dnproto.sdk.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoServer_CreateInviteCode : BaseXrpcCommand
{
    public override IResult GetResponse()
    {
        //
        // Check admin auth
        //
        if(!CheckAdminAuth())
        {
            return Results.Json(new { error = "Unauthorized", message = "Error: Unauthorized" }, statusCode: 401);
        }

        //
        // Get body input
        //
        JsonNode? requestBody = GetRequestBodyAsJson();
        int useCount = 0;
        if(CheckRequestBodyParamInt(requestBody, "useCount", out useCount, minValue: 1, maxValue: 10) == false)
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: Request body must have a valid \"useCount\" property between 1 and 10" }, statusCode: 400);
        }


        //
        // Create invite code
        //
        string? inviteCode = Pds.PdsDb.CreateInviteCode("admin", useCount);
        if(string.IsNullOrEmpty(inviteCode))
        {
            return Results.Json(new { error = "ServerError", message = "Error: Could not create invite code" }, statusCode: 500);
        }

        return Results.Json(new { inviteCode = inviteCode }, statusCode: 200);
    }
}