using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoServer_CreateSession : BaseXrpcCommand
{
    public IResult GetResponse()
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
        ActorInfo? actorInfo = BlueskyClient.ResolveActorInfo(identifier);
        bool actorExists = actorInfo != null;


        //
        // Get account hashed password
        //
        string? storedHashedPassword = Pds.Config.UserHashedPassword;
        bool passwordMatches = PasswordHasher.VerifyPassword(storedHashedPassword, password);


        //
        // Generate JWT tokens
        //
        string? accessJwt = null;
        string? refreshJwt = null;
        if(actorExists && passwordMatches)
        {
            Pds.Logger.LogInfo($"[AUTH] [LEGACY] Successful login. ip={GetCallerIpAddress()} userAgent={GetCallerUserAgent()}");
            accessJwt = JwtSecret.GenerateAccessJwt(actorInfo?.Did, Pds.Config.PdsDid, Pds.Config.JwtSecret);
            refreshJwt = JwtSecret.GenerateRefreshJwt(actorInfo?.Did, Pds.Config.PdsDid, Pds.Config.JwtSecret);

            //
            // Insert into db
            //
            if(string.IsNullOrEmpty(accessJwt) == false && string.IsNullOrEmpty(refreshJwt) == false)
            {
                Pds.PdsDb.CreateLegacySession(accessJwt, refreshJwt);
            }
        }
        else
        {
            Pds.Logger.LogWarning($"[AUTH] [LEGACY] Failed login attempt. ip={GetCallerIpAddress()} userAgent={GetCallerUserAgent()}");
        }


        //
        // Return session info
        //
        return Results.Json(new 
        {
            did = actorInfo?.Did,
            handle = actorInfo?.Handle,
            accessJwt = accessJwt,
            refreshJwt = refreshJwt
        }, 
        statusCode: 200);
    }
}