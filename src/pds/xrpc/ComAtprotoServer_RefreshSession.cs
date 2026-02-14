using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoServer_RefreshSession : BaseXrpcCommand
{
    private static object _refreshLock = new object();

    public IResult GetResponse()
    {
        IncrementStatistics();
        

        //
        // Get the refresh jwt from the caller's Authorization header
        //
        string? originalRefreshJwt = GetAccessJwt();
        if(string.IsNullOrEmpty(originalRefreshJwt))
        {
            return Results.Json(new 
            {
                error = "InvalidRequest",
                message = "Missing refresh token"
            },
            statusCode: 400);
        }


        //
        // Verify refresh jwt
        //
        ClaimsPrincipal? claimsPrincipal = JwtSecret.VerifyRefreshJwt(originalRefreshJwt, Pds.PdsDb.GetConfigProperty("JwtSecret"));
        if (claimsPrincipal == null)
        {
            return Results.Json(new 
            {
                error = "ExpiredToken",
                message = "Token has expired"
            },
            statusCode: 400);
        }

        //
        // Check did
        //
        string? userDid = JwtSecret.GetDidFromClaimsPrincipal(claimsPrincipal);
        if (userDid != Pds.PdsDb.GetConfigProperty("UserDid"))
        {
            return Results.Json(new 
            {
                error = "InvalidToken",
                message = "Token did not match expected user"
            },
            statusCode: 401);
        }


        //
        // Check that refreshJwt exists in db.
        //
        bool refreshJwtExists = false;
        lock(_refreshLock)
        {
            refreshJwtExists = Pds.PdsDb.LegacySessionExistsForRefreshJwt(originalRefreshJwt!);
            Pds.PdsDb.DeleteLegacySessionForRefreshJwt(originalRefreshJwt!);
        }

        if (!refreshJwtExists)
        {
            return Results.Json(new 
            {
                error = "InvalidToken",
                message = "Token not found"
            },
            statusCode: 401);
        }


        //
        // Generate new tokens
        //
        string? handle = Pds.PdsDb.GetConfigProperty("UserHandle");
        string? accessJwt = JwtSecret.GenerateAccessJwt(userDid, Pds.PdsDb.GetConfigProperty("PdsDid"), Pds.PdsDb.GetConfigProperty("JwtSecret"));
        string? newRefreshJwt = JwtSecret.GenerateRefreshJwt(userDid, Pds.PdsDb.GetConfigProperty("PdsDid"), Pds.PdsDb.GetConfigProperty("JwtSecret"));

        if(string.IsNullOrEmpty(accessJwt) || string.IsNullOrEmpty(newRefreshJwt))
        {
            return Results.Json(new 
            {
                error = "ServerError",
                message = "Failed to generate new tokens"
            },
            statusCode: 401);
        }


        //
        // Insert new tokens into db
        //
        var session = new LegacySession
        {
            CreatedDate = PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow),
            AccessJwt = accessJwt,
            RefreshJwt = newRefreshJwt,
            IpAddress = GetCallerIpAddress() ?? "unknown",
            UserAgent = GetCallerUserAgent() ?? "unknown"
        };

        Pds.PdsDb.CreateLegacySession(session);



        //
        // Return session info
        //
        return Results.Json(new 
        {
            did = userDid,
            handle = handle,
            accessJwt = accessJwt,
            refreshJwt = newRefreshJwt
        },
        statusCode: 200);
    }
}