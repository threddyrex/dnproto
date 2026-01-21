using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


/// <summary>
/// Preferences are the exception to the general rule of
/// "proxy requests to /xrpc/app.bsky.* to the AppView"
/// Not sure why.
/// </summary>
public class AppBskyActor_GetPreferences : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        //
        // Require auth
        //
        if(UserIsAuthenticated() == false)
        {
            var (response, statusCode) = GetAuthenticationFailureResponse();
            return Results.Json(response, statusCode: statusCode);
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