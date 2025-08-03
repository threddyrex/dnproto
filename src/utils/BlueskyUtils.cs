using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;

namespace dnproto.utils;

/// <summary>
/// Utility class for Bluesky-related functions.
/// </summary>
public class BlueskyUtils
{
    /// <summary>
    /// Finds a bunch of info for a handle. (did, didDoc, pds)
    ///
    /// Attempts the following steps:
    ///
    ///     1. Resolve handle to did (dns or http).
    ///     2. Resolve did to didDoc. (did:plc or did:web)
    ///     3. Resolve didDoc to pds.
    ///     
    /// </summary>
    /// <param name="handle"></param>
    /// <returns></returns>
    public static Dictionary<string, string> ResolveHandleInfo(string? handle, bool useBlueskyApi = false)
    {
        Dictionary<string, string> ret = new Dictionary<string, string>();

        Console.WriteLine($"ResolveHandleInfo: handle: {handle}");
        if (string.IsNullOrEmpty(handle))
        {
            Console.WriteLine("ResolveHandleInfo: Handle is null or empty. Exiting.");
            return ret;
        }

        //
        // 1. Resolve handle to did (bluesky or dns or http).
        //
        string? did = null;

        if (useBlueskyApi)
        {
            did = ResolveHandleToDid_ViaBlueskyApi(handle);
        }

        if (string.IsNullOrEmpty(did) || !did.StartsWith("did:"))
        {
            did = BlueskyUtils.ResolveHandleToDid_ViaDns(handle);
        }

        if (string.IsNullOrEmpty(did) || !did.StartsWith("did:"))
        {
            did = BlueskyUtils.ResolveHandleToDid_ViaHttp(handle);
        }

        if (string.IsNullOrEmpty(did) || !did.StartsWith("did:")) return ret;
        ret["did"] = did;


        //
        // 2. Resolve did to didDoc. (did:plc or did:web)
        //
        string? didDoc = null;
        if (did.StartsWith("did:plc"))
        {
            didDoc = BlueskyUtils.ResolveDidToDidDoc_DidPlc(did);
        }
        else if (did.StartsWith("did:web"))
        {
            didDoc = BlueskyUtils.ResolveDidToDidDoc_DidWeb(did);
        }
        else
        {
            Console.WriteLine($"ResolveHandleInfo: Unsupported did type: {did}");
            return ret;
        }

        if (string.IsNullOrEmpty(didDoc)) return ret;
        ret["didDoc"] = didDoc;

        Console.WriteLine("didDoc length:");
        Console.WriteLine(didDoc?.Length);


        //
        // 3. Resolve didDoc to pds.
        //
        string? pds = BlueskyUtils.ResolveDidDocToPds(didDoc);

        if (string.IsNullOrEmpty(pds)) return ret;
        ret["pds"] = pds.Replace("https://", "");


        //
        // return
        //
        return ret;

    }



    /// <summary>
    /// Resolves a handle to a did, using dns.
    /// </summary>
    /// <param name="handle">The handle to resolve.</param>
    /// <returns>The resolved did or null if not found.</returns>
    public static string? ResolveHandleToDid_ViaDns(string? handle)
    {
        if (string.IsNullOrEmpty(handle))
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
        if (didDocJson == null) return null;

        string? pds = didDocJson["service"]?.AsArray()?.FirstOrDefault()?["serviceEndpoint"]?.ToString();

        if (string.IsNullOrEmpty(pds)) return null;

        return pds.Replace("https://", "");

    }


    /// <summary>
    /// Gets the profile of an actor.
    /// https://docs.bsky.app/docs/api/app-bsky-actor-get-profile
    /// </summary>
    /// <param name="actor">The actor to get the profile for.</param>
    public static JsonNode? GetProfile(string? actor)
    {
        Console.WriteLine($"GetProfile: actor: {actor}");
        if (string.IsNullOrEmpty(actor))
        {
            Console.WriteLine("GetProfile: Actor is null or empty. Exiting.");
            return null;
        }

        string url = $"https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile?actor={actor}";
        Console.WriteLine($"GetProfile: url: {url}");

        JsonNode? profile = WebServiceClient.SendRequest(url, HttpMethod.Get);

        return profile;
    }


    /// <summary>
    /// Get repo for did.
    /// https://docs.bsky.app/docs/api/com-atproto-sync-get-repo
    /// </summary>
    /// <param name="pds"></param>
    /// <param name="did"></param>
    /// <param name="repoFile"></param>
    public static void GetRepo(string? pds, string? did, string? repoFile)
    {
        Console.WriteLine($"GetRepo: pds: {pds}, did: {did}, repoFile: {repoFile}");

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did) || string.IsNullOrEmpty(repoFile))
        {
            Console.WriteLine("GetRepo: Invalid arguments. Exiting.");
            return;
        }

        string url = $"https://{pds}/xrpc/com.atproto.sync.getRepo?did={did}";
        Console.WriteLine($"GetRepo: url: {url}");

        WebServiceClient.SendRequest(url,
            HttpMethod.Get,
            outputFilePath: repoFile,
            parseJsonResponse: false);

    }


    /// <summary>
    /// List blobs for did.
    /// https://docs.bsky.app/docs/api/com-atproto-sync-list-blobs
    /// </summary>
    /// <param name="pds"></param>
    /// <param name="did"></param>
    /// <returns></returns>
    public static List<string> ListBlobs(string? pds, string? did, string? blobsFile = null, int limit = 100, int sleepMilliseconds = 1000)
    {
        List<string> blobs = new List<string>();
        Console.WriteLine($"ListBlobs: pds: {pds}, did: {did}");
        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did))
        {
            Console.WriteLine("ListBlobs: Invalid arguments. Exiting.");
            return blobs;
        }

        bool keepGoing = true;
        string? cursor = null;

        // We call the api in batches of 100 (or whatever limit is set).
        while (keepGoing)
        {
            string? url = null;

            if (cursor != null)
            {
                url = $"https://{pds}/xrpc/com.atproto.sync.listBlobs?did={did}&limit={limit}&cursor={cursor}";
            }
            else
            {
                url = $"https://{pds}/xrpc/com.atproto.sync.listBlobs?did={did}&limit={limit}";
            }

            Console.WriteLine($"ListBlobs: url: {url}");

            JsonNode? response = WebServiceClient.SendRequest(url, HttpMethod.Get);

            var cids = response?["cids"]?.AsArray();

            keepGoing = cids != null && cids.Count == limit;

            cursor = response?["cursor"]?.ToString();

            if (cids != null)
            {
                Console.WriteLine($"ListBlobs: Count: {cids.Count}");
                Console.WriteLine($"ListBlobs: Cursor: {cursor}");

                foreach (var cid in cids)
                {
                    if (cid == null) continue;
                    blobs.Add(cid.ToString());
                }
            }

            if (keepGoing)
            {
                Thread.Sleep(sleepMilliseconds);
            }
        }

        if (blobsFile != null)
        {
            Console.WriteLine($"ListBlobs: Writing blobs to file: {blobsFile}");
            File.WriteAllLines(blobsFile, blobs);
        }

        return blobs;
    }

    /// <summary>
    /// Get blob for did, by cid.
    /// </summary>
    /// <param name="pds"></param>
    /// <param name="did"></param>
    /// <param name="cid"></param>
    /// <param name="blobFile"></param>
    public static void GetBlob(string? pds, string? did, string? cid, string? blobFile)
    {
        Console.WriteLine($"GetBlob: pds: {pds}, did: {did}, cid: {cid}, blobFile: {blobFile}");

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did) || string.IsNullOrEmpty(cid) || string.IsNullOrEmpty(blobFile))
        {
            Console.WriteLine("GetBlob: Invalid arguments. Exiting.");
            return;
        }

        string url = $"https://{pds}/xrpc/com.atproto.sync.getBlob?did={did}&cid={cid}";
        Console.WriteLine($"GetBlob: url: {url}");

        WebServiceClient.SendRequest(url,
            HttpMethod.Get,
            outputFilePath: blobFile,
            parseJsonResponse: false);

    }

}