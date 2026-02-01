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
        if(UserIsAuthenticated(
            allowedAuthTypes: [AuthType.Legacy, AuthType.Oauth, AuthType.Service], 
            lxm: "com.atproto.repo.uploadBlob") == false)
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
        // Fix content type
        //
        if(string.IsNullOrEmpty(contentType) || contentType == "*/*")
        {
            contentType = FixContentType(blobBytes);
        }

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

    private static string FixContentType(byte[] blobBytes)
    {
        // mp4
        if (blobBytes.Length > 12 &&
            blobBytes[4] == 'f' &&
            blobBytes[5] == 't' &&
            blobBytes[6] == 'y' &&
            blobBytes[7] == 'p' &&
            blobBytes[8] == 'i' &&
            blobBytes[9] == 's' &&
            blobBytes[10] == 'o' &&
            blobBytes[11] == 'm')
        {
            return "video/mp4";
        }

        // mov
        if (blobBytes.Length > 12 &&
            blobBytes[4] == 'f' &&
            blobBytes[5] == 't' &&
            blobBytes[6] == 'y' &&
            blobBytes[7] == 'p' &&
            blobBytes[8] == 'q' &&
            blobBytes[9] == 't' &&
            blobBytes[10] == ' ' &&
            blobBytes[11] == ' ')
        {
            return "video/quicktime";
        }

        // avi
        if (blobBytes.Length > 12 &&
            blobBytes[0] == 0x52 && // R
            blobBytes[1] == 0x49 && // I
            blobBytes[2] == 0x46 && // F
            blobBytes[3] == 0x46 && // F
            blobBytes[8] == 0x41 && // A
            blobBytes[9] == 0x56 && // V
            blobBytes[10] == 0x49)   // I
        {
            return "video/avi";
        }

        //jpg/jpeg
        if (blobBytes.Length > 3 &&
            blobBytes[0] == 0xFF &&
            blobBytes[1] == 0xD8 &&
            blobBytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        //png
        if (blobBytes.Length > 8 &&
            blobBytes[0] == 0x89 &&
            blobBytes[1] == 0x50 &&
            blobBytes[2] == 0x4E &&
            blobBytes[3] == 0x47 &&
            blobBytes[4] == 0x0D &&
            blobBytes[5] == 0x0A &&
            blobBytes[6] == 0x1A &&
            blobBytes[7] == 0x0A)
        {
            return "image/png";
        }

        // gif
        if (blobBytes.Length > 6 &&
            blobBytes[0] == 0x47 && // G
            blobBytes[1] == 0x49 && // I
            blobBytes[2] == 0x46 && // F
            blobBytes[3] == 0x38 && // 8
            (blobBytes[4] == 0x39 || blobBytes[4] == 0x37) && // 9 or 7
            blobBytes[5] == 0x61)   // a
        {
            return "image/gif";
        }

        // webp
        if (blobBytes.Length > 12 &&
            blobBytes[0] == 0x52 && // R
            blobBytes[1] == 0x49 && // I
            blobBytes[2] == 0x46 && // F
            blobBytes[3] == 0x46 && // F
            blobBytes[8] == 0x57 && // W
            blobBytes[9] == 0x45 && // E
            blobBytes[10] == 0x42 && // B
            blobBytes[11] == 0x50)   // P
        {
            return "image/webp";
        }

        // webm
        if (blobBytes.Length > 4 &&
            blobBytes[0] == 0x1A &&
            blobBytes[1] == 0x45 &&
            blobBytes[2] == 0xDF &&
            blobBytes[3] == 0xA3)
        {
            return "video/webm";
        }

        // default
        return "application/octet-stream";
    }
}