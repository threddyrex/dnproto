using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.log;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class AppBsky_Proxy : BaseXrpcCommand
{
    public async Task<IResult> ProxyToAppView(HttpContext context)
    {
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
        // Copy headers from incoming request
        //
        foreach (var header in context.Request.Headers)
        {
            // Skip headers that should not be forwarded
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase) ||
                header.Key.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Atproto-Proxy", StringComparison.OrdinalIgnoreCase))
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