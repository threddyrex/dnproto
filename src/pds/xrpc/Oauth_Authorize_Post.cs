using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.log;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class Oauth_Authorize_Post : BaseXrpcCommand
{
    public async Task<IResult> GetResponse()
    {
        //
        // Get client_id and request_uri from query parameters
        //
        JsonNode? requestBody = GetRequestBodyAsJson();
        string? clientId = null, requestUri = null, userName = null, password = null;

        if(CheckRequestBodyParam(requestBody, "client_id", out clientId) == false
            || CheckRequestBodyParam(requestBody, "request_uri", out requestUri) == false
            || CheckRequestBodyParam(requestBody, "username", out userName) == false
            || CheckRequestBodyParam(requestBody, "password", out password) == false)
        {
            Pds.Logger.LogWarning($"[OAUTH] Missing parameters. client_id={clientId} request_uri={requestUri} username={userName}");
            return Results.Json(new{}, statusCode: 401);            
        }

        //
        // Load 
        //
        if(!Pds.PdsDb.OauthRequestExists(requestUri!))
        {
            Pds.Logger.LogWarning($"[OAUTH] Oauth request does not exist or has expired. request_uri={requestUri}");
            return Results.Json(new{}, statusCode: 401);
        }

        OauthRequest oauthRequest = Pds.PdsDb.GetOauthRequest(requestUri!);


        //
        // Resolve actor info and check password
        //
        ActorInfo? actorInfo = BlueskyClient.ResolveActorInfo(userName!);
        bool actorExists = actorInfo != null;
        string? storedHashedPassword = Pds.Config.UserHashedPassword;
        bool passwordMatches = PasswordHasher.VerifyPassword(storedHashedPassword, password);
        bool authSucceeded = actorExists && passwordMatches;

        if(authSucceeded == false)
        {
            Pds.Logger.LogWarning($"[OAUTH] Authentication failed. username={userName}");
            return Results.Content(Oauth_Authorize_Get.GetHtmlForAuthForm(requestUri!, clientId!, oauthRequest, true), "text/html");
        }



        return Results.Json(new{}, statusCode: 401);            

    }
}