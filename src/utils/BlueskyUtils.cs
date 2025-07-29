using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;

namespace dnproto.utils;

public class BlueskyUtils
{
    /// <summary>
    /// Resolves a handle to a did, using dns.
    /// </summary>
    /// <param name="handle">The handle to resolve.</param>
    /// <returns>The resolved did or null if not found.</returns>
    public static string? ResolveHandleToDid_ViaDns(string? handle)
    {
        if(string.IsNullOrEmpty(handle))
        {
            Console.WriteLine("ResolveHandleToDid_ViaDns: Handle is null or empty.");
            return null;
        }

        string? did = null;
        string url = $"https://cloudflare-dns.com/dns-query?name=_atproto.{handle}&type=TXT";

        Console.WriteLine($"ResolveHandleToDid_ViaDns: handle: {handle}");
        Console.WriteLine($"ResolveHandleToDid_ViaDns: url: {url}");

        JsonNode? response = WebServiceClient.SendRequest(url, HttpMethod.Get, acceptHeader: "application/dns-json");

        if (response != null) 
        {
            did = response["Answer"]?.AsArray()?.FirstOrDefault()?.AsObject()?["data"]?.ToString().Replace("\"", "").Replace("did=", "");
        }

        Console.WriteLine($"ResolveHandleToDid_ViaDns: did: {did}");

        return did;
    }


    /// <summary>
    /// Resolves a handle to a did, using http.
    /// </summary>
    /// <param name="handle">The handle to resolve.</param>
    /// <returns>The resolved did or null if not found.</returns>
    public static string? ResolveHandleToDid_ViaHttp(string? handle)
    {
        string? did = null;
        string url = $"https://{handle}/.well-known/atproto-did";

        Console.WriteLine($"ResolveHandleToDid_ViaHttp: handle: {handle}");
        Console.WriteLine($"ResolveHandleToDid_ViaHttp: url: {url}");

        var responseText = WebServiceClient.SendRequestEx(url, HttpMethod.Get);

        if (responseText != null)
        {
            did = responseText;
        }

        Console.WriteLine($"ResolveHandleToDid_ViaHttp: did: {did}");

        return did;
    }


    /// <summary>
    /// Resolves a handle to a did, using the Bluesky public api.
    /// </summary>
    /// <param name="handle">The handle to resolve.</param>
    /// <returns>The resolved did or null if not found.</returns>
    public static string? ResolveHandleToDid_ViaBlueskyApi(string? handle)
    {
        Console.WriteLine($"ResolveHandleToDid_ViaBlueskyApi: handle: {handle}");

        if (string.IsNullOrEmpty(handle))
        {
            Console.WriteLine("ResolveHandleToDid_ViaBlueskyApi: Handle is null or empty. Exiting.");
            return null;
        }

        string url = $"https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle?handle={handle}";
        Console.WriteLine($"ResolveHandleToDid_ViaBlueskyApi: url: {url}");

        JsonNode? response = WebServiceClient.SendRequest(url, HttpMethod.Get);

        string? did = JsonData.SelectString(response, "did");

        Console.WriteLine($"ResolveHandleToDid_ViaBlueskyApi: did: {did}");
        return did;
    }


    /// <summary>
    /// Resolves a did to didDoc, for did:plc.
    /// </summary>
    /// <param name="did">The did to resolve.</param>
    /// <returns>The resolved didDoc or null if not found.</returns>
    public static string? ResolveDidToDidDoc_DidPlc(string? did)
    {
        Console.WriteLine($"ResolveDidToDidDoc_DidPlc: did: {did}");

        if (string.IsNullOrEmpty(did) || !did.StartsWith("did:plc"))
        {
            Console.WriteLine($"ResolveDidToDidDoc_DidPlc: invalid did, exiting.");
            return null;
        }

        string? didDoc = null;
        string url = $"https://plc.directory/{did}";
        Console.WriteLine($"ResolveDidToDidDoc_DidPlc: url: {url}");

        var response = WebServiceClient.SendRequest(url, HttpMethod.Get);

        didDoc = JsonData.ConvertToJsonString(response);


        return didDoc;
    }

    /// <summary>
    /// Resolves a did to didDoc, for did:web.
    /// </summary>
    /// <param name="did">The did to resolve.</param>
    /// <returns>The resolved didDoc or null if not found.</returns>
    public static string? ResolveDidToDidDoc_DidWeb(string? did)
    {
        Console.WriteLine($"ResolveDidToDidDoc_DidWeb: did: {did}");

        if (string.IsNullOrEmpty(did) || !did.StartsWith("did:web:"))
        {
            Console.WriteLine($"ResolveDidToDidDoc_DidWeb: invalid did, exiting.");
            return null;
        }

        string hostname = did.Replace("did:web:", "");
        string url = $"https://{hostname}/.well-known/did.json";
        Console.WriteLine($"ResolveDidToDidDoc_DidWeb: url: {url}");

        var response = WebServiceClient.SendRequest(url, HttpMethod.Get);

        var didDoc = JsonData.ConvertToJsonString(response);

        return didDoc;
    }

    /// <summary>
    /// Resolves a didDoc to pds.
    /// This is used to get the pds from the didDoc.
    /// </summary>
    /// <param name="didDoc">The didDoc to resolve.</param>
    /// <returns>The resolved pds or null if not found.</returns>
    public static string? ResolveDidDocToPds(string? didDoc)
    {
        Console.WriteLine($"ResolveDidDocToPds: didDoc length: {didDoc?.Length}");

        if (string.IsNullOrEmpty(didDoc))
        {
            Console.WriteLine("DidDoc is null or empty.");
            return null;
        }

        JsonNode? didDocJson = JsonNode.Parse(didDoc);
        if(didDocJson == null) return null;

        string? pds = didDocJson["service"]?.AsArray()?.FirstOrDefault()?["serviceEndpoint"]?.ToString();

        if(string.IsNullOrEmpty(pds)) return null;

        return pds.Replace("https://", "");

    }
}