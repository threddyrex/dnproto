using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.log;
using dnproto.repo;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


/// <summary>
/// Preferences are the exception to the general rule of
/// "proxy requests to /xrpc/app.bsky.* to the AppView"
/// Not sure why.
/// </summary>
public class AppBskyActor_PutPreferences : BaseXrpcCommand
{
    public async Task<IResult> GetResponseAsync()
    {
        IncrementStatistics();
        
        //
        // Require auth
        //
        if(UserIsAuthenticated() == false)
        {
            var (response, statusCode) = GetAuthenticationFailureResponse();
            return Results.Json(response, statusCode: statusCode);
        }




        //
        // Read content type, content length, and blob bytes from request
        //
        string? contentType = HttpContext.Request.ContentType;
        int contentLength = (int)(HttpContext.Request.ContentLength ?? 0);
        byte[] blobBytes = new byte[contentLength];
        await HttpContext.Request.Body.ReadExactlyAsync(blobBytes, 0, contentLength);



        //
        // Read as json
        //
        JsonNode? prefsJson = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(blobBytes));
        if(prefsJson == null)
        {
            return Results.Json(new { error = "InvalidRequest", message = "Failed to parse preferences json" }, statusCode: 400);
        }

        //
        // Insert or update into db
        //
        if(Pds.PdsDb.GetPreferencesCount() == 0)
        {
            Pds.PdsDb.InsertPreferences(prefsJson.ToJsonString());
        }
        else
        {
            Pds.PdsDb.UpdatePreferences(prefsJson.ToJsonString());
        }

        //
        // Return ok
        //
        return Results.Json(new { message = "Preferences updated" },
        statusCode: 200);
    }
}