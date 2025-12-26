

using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoIdentity_ResolveHandle : BaseXrpcCommand
{
    public override IResult GetResponse()
    {
        //
        // Get param
        //
        string? actor = HttpContext.Request.Query.ContainsKey("handle") ? (string?) HttpContext.Request.Query["handle"] : null;

        if(string.IsNullOrEmpty(actor))
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: Params must have the property \"handle\"" }, statusCode: 400);
        }


        //
        // Resolve actor
        //
        ActorInfo? actorInfo = BlueskyClient.ResolveActorInfo(actor, useBsky: false, resolveDidDoc: false);


        //
        // Validate response.
        //
        if(actorInfo == null)
        {
            return Results.Json(new { error = "NotFound", message = "Error: Handle not found" }, statusCode: 404);
        }

        if(string.IsNullOrEmpty(actorInfo.Did))
        {
            return Results.Json(new { error = "NotFound", message = "Error: Handle not found" }, statusCode: 404);
        }

        //
        // Return success
        //
        return Results.Json(new { did = actorInfo.Did }, statusCode: 200);
    }
}