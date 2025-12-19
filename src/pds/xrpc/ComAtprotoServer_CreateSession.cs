using System.Text.Json.Nodes;
using dnproto.sdk.auth;
using dnproto.sdk.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoServer_CreateSession : BaseXrpcCommand
{
    public override IResult GetResponse()
    {

        //
        // Get body input
        //
        JsonNode? requestBody = GetRequestBodyAsJson();
        string? identifier, password;

        if(!CheckRequestBodyParam(requestBody, "identifier", out identifier)
            || ! CheckRequestBodyParam(requestBody, "password", out password)
            || string.IsNullOrEmpty(identifier)
            || string.IsNullOrEmpty(password)
        )
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: invalid params." }, statusCode: 400);
        }


        //
        // Resolve actor info
        //
        ActorInfo? actorInfo = BlueskyClient.ResolveActorInfo(identifier, useBsky: false);
        bool actorExists = actorInfo != null;

        //
        // Get account hashed password
        //
        string? storedHashedPassword = Pds.PdsDb.GetAccountHashedPassword(actorInfo?.Did);
        bool passwordMatches = PasswordHasher.VerifyPassword(storedHashedPassword, password);

        //
        // Return info
        //
        return Results.Json(new 
        {
            actorExists = actorExists,
            hashedPassword = storedHashedPassword,
            passwordMatches = passwordMatches
        }, 
        statusCode: 200);
    }
}