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
        if(requestBody == null)
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: Request body is not valid JSON" }, statusCode: 400);
        }

        string? useCountStr = requestBody["useCount"]?.ToString();

        int useCount = 0;
        if(string.IsNullOrEmpty(useCountStr) || !int.TryParse(useCountStr, out useCount))
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: Request body must have a valid \"useCount\" property" }, statusCode: 400);
        }

        if(useCount < 1 || useCount > 10)
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: \"useCount\" must be between 1 and 10" }, statusCode: 400);
        }


        //
        // Create invite code
        //
        string? inviteCode = Pds.PdsDb.CreateInviteCode(useCount);
        if(string.IsNullOrEmpty(inviteCode))
        {
            return Results.Json(new { error = "ServerError", message = "Error: Could not create invite code" }, statusCode: 500);
        }

        return Results.Json(new { inviteCode = inviteCode }, statusCode: 200);
    }
}