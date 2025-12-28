using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class AppBskyActor_GetPreferences : BaseXrpcCommand
{
    public IResult GetResponse()
    {

        //
        // Get the jwt from the caller's Authorization header
        //
        string? accessJwt = GetAccessJwt();
        ClaimsPrincipal? claimsPrincipal = JwtSecret.VerifyAccessJwt(accessJwt, Pds.Config.JwtSecret);

        string? userDid = JwtSecret.GetDidFromClaimsPrincipal(claimsPrincipal);
        bool didMatches = userDid == Pds.Config.UserDid;

        if(!didMatches)
        {
            // return 204
            return Results.Json(new { }, statusCode: 204);
        }

        //
        // Get preferences from PDS config
        //
        if(Pds.PdsDb.GetPreferencesCount() == 0)
        {
            // return 204
            return Results.Json(new { }, statusCode: 204);
        }
        string? prefsJson = Pds.PdsDb.GetPreferences();

        //
        // Return prefs json
        //
        return Results.Json(JsonNode.Parse(prefsJson),
        statusCode: 200);
    }
}