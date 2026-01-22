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
        if(!Pds.Config.OauthIsEnabled)
        {
            return Results.Json(new{}, statusCode: 403);
        }

        //
        // Get form data
        //
        string? body = HttpContext.Request.Body != null ? await new StreamReader(HttpContext.Request.Body).ReadToEndAsync() : null;
        if (string.IsNullOrEmpty(body))
        {
            Pds.Logger.LogWarning($"[OAUTH] Missing form data.");
            return Results.Json(new{}, statusCode: 401);
        }

        string? clientId = null, requestUri = null, userName = null, password = null;

        if(CheckFormDataParam(body, "client_id", out clientId) == false
            || CheckFormDataParam(body, "request_uri", out requestUri) == false
            || CheckFormDataParam(body, "username", out userName) == false
            || CheckFormDataParam(body, "password", out password) == false)
        {
            Pds.Logger.LogWarning($"[OAUTH] Missing parameters. client_id={clientId} request_uri={requestUri} username={userName}");
            return Results.Json(new{}, statusCode: 401);            
        }


        //
        // Load the oauth request
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
            Pds.Logger.LogWarning($"[OAUTH] Authentication failed. username={userName} actorExists={actorExists} passwordMatches={passwordMatches}");
            return Results.Content(Oauth_Authorize_Get.GetHtmlForAuthForm(requestUri!, clientId!, oauthRequest, true), "text/html");
        }
        else
        {
            Pds.Logger.LogInfo($"[OAUTH] Authentication succeeded. username={userName} actorExists={actorExists} passwordMatches={passwordMatches}");            
        }



        return Results.Json(new{}, statusCode: 401);            

    }
}