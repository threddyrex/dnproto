using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.log;
using dnproto.repo;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoRepo_UploadBlob : BaseXrpcCommand
{
    public async Task<IResult> GetResponseAsync()
    {
        //
        // Get the jwt from the caller's Authorization header
        //
        string? accessJwt = GetAccessJwt();
        ClaimsPrincipal? claimsPrincipal = JwtSecret.VerifyAccessJwt(accessJwt, Pds.Config.JwtSecret);

        string? userDid = JwtSecret.GetDidFromClaimsPrincipal(claimsPrincipal);
        bool didMatches = userDid == Pds.Config.UserDid;

        if(didMatches == false)
        {
            return Results.Json(new { error = "InvalidRequest", message = "Need auth" }, statusCode: 204);
        }


        //
        // Read content type, content length, and blob bytes from request
        //
        string? contentType = HttpContext.Request.ContentType;
        int contentLength = (int)(HttpContext.Request.ContentLength ?? 0);
        byte[] blobBytes = new byte[contentLength];
        await HttpContext.Request.Body.ReadExactlyAsync(blobBytes, 0, contentLength);


        //
        // Generate cid
        //
        string cid = CidV1.GenerateForBlobBytes(blobBytes).Base32;

        //
        // Update or insert
        //
        var blob = new dnproto.pds.db.Blob
        {
            Cid = cid,
            ContentType = contentType ?? string.Empty,
            ContentLength = contentLength,
            Bytes = blobBytes
        };

        if(Pds.PdsDb.BlobExists(cid))
        {
            Pds.PdsDb.UpdateBlob(blob);
        }
        else
        {
            Pds.PdsDb.InsertBlob(blob);
        }

        //
        // Return session info
        // ex: {"blob":{"$type":"blob","ref":{"$link":"bafkreihcduyzpj4kzp2pbusw7lz5h3eud33y4uvahjqqn73xxnkegel5lq"},"mimeType":"image/jpeg","size":45744}}
        //
        var responseObj = new JsonObject
        {
            ["blob"] = new JsonObject
            {
                ["$type"] = "blob",
                ["ref"] = new JsonObject
                {
                    ["$link"] = cid
                },
                ["mimeType"] = contentType,
                ["size"] = contentLength
            }
        };

        Pds.Logger.LogInfo($"Uploaded blob cid={cid} contentType={contentType} contentLength={contentLength} userDid={userDid}");
        return Results.Json(responseObj, statusCode: 200);
    }
}