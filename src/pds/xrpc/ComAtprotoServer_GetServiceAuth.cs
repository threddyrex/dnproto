using dnproto.auth;
using dnproto.repo;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


/// <summary>
/// Get a signed token on behalf of the requesting DID for the requested service.
/// https://docs.bsky.app/docs/api/com-atproto-server-get-service-auth
/// </summary>
public class ComAtprotoServer_GetServiceAuth : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        //
        // Require auth
        //
        if (UserIsAuthenticated() == false)
        {
            var (response, statusCode) = GetAuthenticationFailureResponse();
            return Results.Json(response, statusCode: statusCode);
        }

        //
        // Get required aud parameter (DID of service that will receive the token)
        //
        string? aud = HttpContext.Request.Query["aud"];
        if (string.IsNullOrEmpty(aud))
        {
            return Results.Json(new { error = "InvalidRequest", message = "Missing required parameter: aud" }, statusCode: 400);
        }

        //
        // Get optional lxm parameter (lexicon method to bind token to)
        //
        string? lxm = HttpContext.Request.Query["lxm"];

        //
        // Get optional exp parameter (expiry in Unix epoch seconds, defaults to 60 seconds in future)
        //
        int expiresInSeconds = 60;
        string? expParam = HttpContext.Request.Query["exp"];
        if (!string.IsNullOrEmpty(expParam))
        {
            if (long.TryParse(expParam, out long expUnix))
            {
                // exp is Unix epoch time - convert to seconds from now
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                expiresInSeconds = (int)(expUnix - now);
                
                // Clamp to reasonable bounds
                if (expiresInSeconds < 1)
                {
                    return Results.Json(new { error = "InvalidRequest", message = "exp must be in the future" }, statusCode: 400);
                }
                if (expiresInSeconds > 300) // Services may enforce max bounds
                {
                    expiresInSeconds = 300;
                }
            }
            else
            {
                return Results.Json(new { error = "InvalidRequest", message = "Invalid exp parameter" }, statusCode: 400);
            }
        }

        //
        // Get signing keys from config
        //
        var signingKeyPrivateMultibase = Pds.Config.UserPrivateKeyMultibase;
        var signingKeyPublicMultibase = Pds.Config.UserPublicKeyMultibase;

        // Convert from multibase to hex for Signer.SignToken
        var privateKeyWithPrefix = Base58BtcEncoding.DecodeMultibase(signingKeyPrivateMultibase);
        var publicKeyWithPrefix = Base58BtcEncoding.DecodeMultibase(signingKeyPublicMultibase);

        // Remove multicodec prefix (0x86 0x26 for P-256 private, 0x80 0x24 for P-256 public)
        byte[] privateKeyBytes = privateKeyWithPrefix.Skip(2).ToArray();
        byte[] publicKeyBytes = publicKeyWithPrefix.Skip(2).ToArray();

        string privateKeyHex = Convert.ToHexString(privateKeyBytes).ToLowerInvariant();
        string publicKeyHex = Convert.ToHexString(publicKeyBytes).ToLowerInvariant();

        //
        // Create claims - lxm is optional but should be included if specified
        //
        Dictionary<string, string>? claims = null;
        if (!string.IsNullOrEmpty(lxm))
        {
            claims = new Dictionary<string, string>
            {
                { "lxm", lxm }
            };
        }

        //
        // Sign the service auth token
        //
        string token = Signer.SignToken(
            publicKeyHex,
            privateKeyHex,
            Pds.Config.UserDid,  // iss: the requesting user's DID
            aud,                 // aud: the service DID that will validate this token
            claims,
            expiresInSeconds,
            Pds.Logger
        );

        //
        // Return the token
        //
        return Results.Json(new { token }, statusCode: 200);
    }
}
