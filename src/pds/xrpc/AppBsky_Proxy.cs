using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.log;
using dnproto.repo;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class AppBsky_Proxy : BaseXrpcCommand
{
    public async Task<IResult> ProxyToAppView(HttpContext context)
    {
        //
        // Get authenticated user DID from access token
        //
        string? accessJwt = GetAccessJwt();
        ClaimsPrincipal? claimsPrincipal = JwtSecret.VerifyAccessJwt(accessJwt, Pds.Config.JwtSecret);
        string? authedDid = JwtSecret.GetDidFromClaimsPrincipal(claimsPrincipal);

        if (string.IsNullOrEmpty(authedDid))
        {
            Pds.Logger.LogError("No authenticated user found");
            return Results.Problem("Authentication required", statusCode: 401);
        }

        //
        // Figure out atproto proxy
        //
        const string defaultAtprotoProxyValue = "did:web:api.bsky.app#bsky_appview";

        // if we have atproto proxy in headers, use that
        string atprotoProxyValue = context.Request.Headers.ContainsKey("Atproto-Proxy")
            ? context.Request.Headers["Atproto-Proxy"].ToString()
            : defaultAtprotoProxyValue;

        AtProtoProxy? atprotoProxy = AtProtoProxy.FromHeader(atprotoProxyValue);

        if(atprotoProxy == null)
        {
            Pds.Logger.LogError("Invalid Atproto-Proxy header value");
            return Results.Problem("Invalid Atproto-Proxy header value", statusCode: 400);
        }

        //
        // Resolve did doc
        //
        ActorInfo? actorInfo = Pds.LocalFileSystem.ResolveActorInfo(atprotoProxy.Did);
        if(actorInfo == null || actorInfo.DidDoc == null)
        {
            Pds.Logger.LogError($"Unable to resolve actor info for DID: {atprotoProxy.Did}");
            return Results.Problem("Unable to resolve actor info for DID", statusCode: 400);
        }
        JsonNode? didDocNode = JsonNode.Parse(actorInfo.DidDoc);
        if(didDocNode == null)
        {
            Pds.Logger.LogError($"Unable to parse DID Document for DID: {atprotoProxy.Did}");
            return Results.Problem("Unable to parse DID Document for DID", statusCode: 400);
        }

        //
        // Look through did doc to find service endpoint for service id
        //
        string? serviceEndpoint = null;
        foreach(var serviceNode in didDocNode["service"]!.AsArray())
        {
            if(serviceNode?["id"] != null && serviceNode["id"]!.ToString() == $"#{atprotoProxy.ServiceId}")
            {
                string? serviceEndpointCandidate = serviceNode["serviceEndpoint"]?.ToString();
                if(!string.IsNullOrEmpty(serviceEndpointCandidate))
                {
                    serviceEndpoint = serviceEndpointCandidate;
                    Pds.Logger.LogInfo($"Resolved service endpoint for {atprotoProxy.Did}#{atprotoProxy.ServiceId} to {serviceEndpoint}");
                }
            }
        }
        if(string.IsNullOrEmpty(serviceEndpoint))
        {
            Pds.Logger.LogError($"Unable to find service endpoint for {atprotoProxy.Did}#{atprotoProxy.ServiceId}");
            return Results.Problem("Unable to find service endpoint in DID Document", statusCode: 400);
        }


        string appViewUrl = serviceEndpoint;
        var path = context.Request.Path.Value;
        var queryString = context.Request.QueryString.Value;
        var targetUrl = $"{appViewUrl}{path}{queryString}";

        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);

        //
        // Create service authentication JWT
        //
        // Extract the lexicon method from the path (remove /xrpc/ prefix)
        string lxm = context.Request.Path.Value?.TrimStart('/') ?? string.Empty;
        if (lxm.StartsWith("xrpc/", StringComparison.OrdinalIgnoreCase))
        {
            lxm = lxm.Substring(5); // Remove "xrpc/" prefix
        }
        Pds.Logger.LogInfo($"Creating service auth JWT for lxm: {lxm}");
        var serviceDid = atprotoProxy.Did;

        // Get signing keys for the authenticated user
        // Note: In a multi-user system, you would look up the user's signing key from a database
        // For now, we use the PDS user's keys from config
        string signingKeyPublicMultibase = Pds.Config.UserPublicKeyMultibase;
        string signingKeyPrivateMultibase = Pds.Config.UserPrivateKeyMultibase;

        if (string.IsNullOrEmpty(signingKeyPrivateMultibase) || string.IsNullOrEmpty(signingKeyPublicMultibase))
        {
            Pds.Logger.LogError($"Signing key not found for DID: {authedDid}");
            return Results.Problem("Signing key not found", statusCode: 500);
        }

        // Convert multibase keys to hex format for SignToken
        var privateKeyWithPrefix = Base58BtcEncoding.DecodeMultibase(signingKeyPrivateMultibase);
        var publicKeyWithPrefix = Base58BtcEncoding.DecodeMultibase(signingKeyPublicMultibase);

        // Remove multicodec prefix (0x86 0x26 for P-256 private, 0x80 0x24 for P-256 public)
        byte[] privateKeyBytes = privateKeyWithPrefix.Skip(2).ToArray();
        byte[] publicKeyBytes = publicKeyWithPrefix.Skip(2).ToArray();

        string privateKeyHex = Convert.ToHexString(privateKeyBytes).ToLowerInvariant();
        string publicKeyHex = Convert.ToHexString(publicKeyBytes).ToLowerInvariant();

        Pds.Logger.LogTrace($"Service auth - authedDid: {authedDid}, serviceDid: {serviceDid}");
        Pds.Logger.LogTrace($"Service auth - publicKeyHex: {publicKeyHex}");
        Pds.Logger.LogTrace($"Service auth - publicKeyMultibase: {signingKeyPublicMultibase}");

        // Create JWT for service authentication
        var claims = new Dictionary<string, string>
        {
            { "lxm", lxm }
        };

        string serviceAuthJwt = Signer.SignToken(
            publicKeyHex,
            privateKeyHex,
            authedDid,      // iss: issuer is the authenticated user
            serviceDid,     // aud: audience is the service DID
            claims,
            300,            // exp: 5 minutes (300 seconds)
            Pds.Logger      // logger for debugging
        );

        Pds.Logger.LogTrace($"Service auth JWT created: {serviceAuthJwt}");

        // Verify the signature we just created (for debugging)
        var verifyResult = Signer.ValidateToken(serviceAuthJwt, signingKeyPublicMultibase, authedDid, serviceDid, Pds.Logger);
        if (verifyResult == null)
        {
            Pds.Logger.LogError("Self-verification of service auth JWT failed!");
        }
        else
        {
            Pds.Logger.LogTrace("Self-verification of service auth JWT succeeded");
        }

        // Add Authorization header with the service auth JWT
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {serviceAuthJwt}");

        //
        // Copy headers from incoming request
        //
        foreach (var header in context.Request.Headers)
        {
            // Skip headers that should not be forwarded
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                header.Key.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Atproto-Proxy", StringComparison.OrdinalIgnoreCase) ||
                (header.Key.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase)
                    && header.Value.ToString().Contains("gzip")))
                continue;

            request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        //
        // Copy request body for POST requests
        //
        if (context.Request.Method == "POST" && context.Request.ContentLength > 0)
        {
            var bodyContent = new StreamContent(context.Request.Body);
            if (context.Request.ContentType != null)
            {
                bodyContent.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }
            request.Content = bodyContent;
        }

        try
        {
            Pds.Logger.LogTrace($"REQUEST:\n{request}");
            var response = await httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            Pds.Logger.LogTrace($"RESPONSE: {response}");

            // Copy response headers
            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            context.Response.StatusCode = (int)response.StatusCode;

            if (!string.IsNullOrEmpty(responseBody))
            {
                return Results.Content(responseBody, response.Content.Headers.ContentType?.ToString());
            }
            else
            {
                return Results.StatusCode((int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            Pds.Logger.LogError($"Error proxying to AppView: {ex.Message}");
            return Results.Problem("Error proxying request to AppView", statusCode: 502);
        }
    }
}