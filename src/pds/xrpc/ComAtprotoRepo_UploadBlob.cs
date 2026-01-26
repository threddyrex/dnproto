using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.repo;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoRepo_UploadBlob : BaseXrpcCommand
{
    public async Task<IResult> GetResponseAsync()
    {
        //
        // Require auth
        //
        if(UserIsAuthenticated() == false 
            && ServiceAuthIsAuthenticated(lxm: "com.atproto.repo.uploadBlob") == false)
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
        // Generate cid
        //
        string cid = CidV1.GenerateForBlobBytes(blobBytes).Base32;

        //
        // Update or insert
        //
        var blob = new Blob
        {
            Cid = cid,
            ContentType = contentType ?? string.Empty,
            ContentLength = contentLength
        };

        if(Pds.PdsDb.BlobExists(cid))
        {
            Pds.PdsDb.UpdateBlob(blob);
            Pds.blobDb.UpdateBlobBytes(cid, blobBytes);
        }
        else
        {
            Pds.PdsDb.InsertBlob(blob);
            Pds.blobDb.InsertBlobBytes(cid, blobBytes);
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

        Pds.Logger.LogInfo($"Uploaded blob cid={cid} contentType={contentType} contentLength={contentLength} userDid={Pds.Config.UserDid}");
        return Results.Json(responseObj, statusCode: 200);
    }
}