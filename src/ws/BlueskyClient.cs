using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.log;
using dnproto.uri;

namespace dnproto.ws;

/// <summary>
/// Entry point for interacting with this SDK.
/// </summary>
public class BlueskyClient
{
    public static BaseLogger Logger = new NullLogger();

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
    public static ActorInfo ResolveActorInfo(string? actor)
    {
        var ret = new ActorInfo();
        ret.Actor = actor;

        Logger.LogTrace($"ResolveActorInfo: actor: {actor}");
        if (string.IsNullOrEmpty(actor))
        {
            Logger.LogTrace("ResolveActorInfo: actor is null or empty. Exiting.");
            return ret;
        }

        //
        // 1. Resolve handle to did. Call all three methods (bluesky api, dns, http).
        //
        if(actor.StartsWith("did:"))
        {
            ret.Did = actor;
            Logger.LogTrace("Actor is already a did.");
        }
        else
        {
            ret.Handle = actor;
            Logger.LogTrace("Actor is not a did, resolving to did.");
            ret.Did_Bsky = ResolveHandleToDid_ViaBlueskyApi(actor);
            ret.Did_Dns = ResolveHandleToDid_ViaDns(actor);
            ret.Did_Http = ResolveHandleToDid_ViaHttp(actor);
            ret.Did = ret.Did_Bsky ?? ret.Did_Dns ?? ret.Did_Http;
        }

        if (string.IsNullOrEmpty(ret.Did) || !ret.Did.StartsWith("did:")) return ret;


        //
        // 2. Resolve did to didDoc. (did:plc or did:web)
        //
        ret.DidDoc = ResolveDidToDidDoc(ret.Did);
        if (string.IsNullOrEmpty(ret.DidDoc)) return ret;

        Logger.LogTrace("didDoc length: " + ret.DidDoc?.Length);


        //
        // 3. Resolve didDoc to pds.
        //
        ret.Pds = BlueskyClient.ResolveDidDocToPds(ret.DidDoc);

        if (string.IsNullOrEmpty(ret.Pds)) return ret;
        ret.Pds = ret.Pds.Replace("https://", "");


        //
        // 4. Get handle from diddoc
        //
        if (ret.DidDoc != null && string.IsNullOrEmpty(ret.Handle))
        {
            string? handleFromDidDoc = null;
            JsonNode? didDocJson = JsonNode.Parse(ret.DidDoc);
            handleFromDidDoc = didDocJson?["alsoKnownAs"]?.AsArray()?.FirstOrDefault()?.ToString()?.Replace("at://", "")?.Split('/')?[0];
            ret.Handle = handleFromDidDoc;
        }

        //
        // return
        //
        return ret;

    }


    /// <summary>
    /// Resolves a did to a didDoc.
    /// For did:plc, go to the plc directory. 
    /// For did:web, resolve the doc via HTTP at the .well-known endpoint.
    /// </summary>
    /// <param name="did">The did to resolve.</param>
    /// <returns>The resolved didDoc or null if not found.</returns>
    public static string? ResolveDidToDidDoc(string? did)
    {
        if(did == null) return null;

        string? didDoc = null;
        if (did.StartsWith("did:plc"))
        {
            didDoc = BlueskyClient.ResolveDidToDidDoc_DidPlc(did);
        }
        else if (did.StartsWith("did:web"))
        {
            didDoc = BlueskyClient.ResolveDidToDidDoc_DidWeb(did);
        }

        return didDoc;
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
            Logger.LogTrace("ResolveHandleToDid_ViaDns: Handle is null or empty.");
            return null;
        }

        string? did = null;
        string url = $"https://cloudflare-dns.com/dns-query?name=_atproto.{handle}&type=TXT";

        Logger.LogTrace($"ResolveHandleToDid_ViaDns: handle: {handle}");
        Logger.LogTrace($"ResolveHandleToDid_ViaDns: url: {url}");

        JsonNode? response = BlueskyClient.SendRequest(url, HttpMethod.Get, acceptHeader: "application/dns-json");

        if (response != null)
        {
            // print response for debugging
            var options = new JsonSerializerOptions { WriteIndented = true };
            Logger.LogTrace(response.ToJsonString(options));

            // get did
            foreach (var answer in response["Answer"]?.AsArray() ?? new JsonArray())
            {
                if(answer == null) continue;
                var dataRaw = answer.AsObject()?["data"]?.ToString();
                var data = dataRaw?.Replace("\"", "");

                Logger.LogTrace($"dataRaw: {dataRaw}");
                Logger.LogTrace($"data: {data}");

                if (string.IsNullOrEmpty(data)) continue;
                
                if(data.StartsWith("did="))
                {
                    did = data.Replace("did=", "");
                    break;
                }
            }
        }

        Logger.LogTrace($"ResolveHandleToDid_ViaDns: did: {did}");

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

        Logger.LogTrace($"ResolveHandleToDid_ViaHttp: handle: {handle}");
        Logger.LogTrace($"ResolveHandleToDid_ViaHttp: url: {url}");

        string? responseText = null;

        try
        {
            responseText = BlueskyClient.SendRequestEx(url, HttpMethod.Get);
        }
        catch (Exception ex)
        {
            Exception? inner = ex;
            int count = 1;
            while (inner != null)
            {
                Logger.LogTrace($"ResolveHandleToDid_ViaHttp: Exception {count}: {inner.Message}");
                Logger.LogTrace(inner.StackTrace ?? "");
                inner = inner.InnerException;
                count++;
            }
        }

        if (responseText != null && responseText.StartsWith("did:"))
        {
            did = responseText;
        }

        Logger.LogTrace($"ResolveHandleToDid_ViaHttp: did: {did}");

        return did;
    }


    /// <summary>
    /// Resolves a handle to a did, using the Bluesky public api.
    /// </summary>
    /// <param name="handle">The handle to resolve.</param>
    /// <returns>The resolved did or null if not found.</returns>
    public static string? ResolveHandleToDid_ViaBlueskyApi(string? handle)
    {
        Logger.LogTrace($"ResolveHandleToDid_ViaBlueskyApi: handle: {handle}");

        if (string.IsNullOrEmpty(handle))
        {
            Logger.LogTrace("ResolveHandleToDid_ViaBlueskyApi: Handle is null or empty. Exiting.");
            return null;
        }

        string url = $"https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle?handle={handle}";
        Logger.LogTrace($"ResolveHandleToDid_ViaBlueskyApi: url: {url}");

        JsonNode? response = BlueskyClient.SendRequest(url, HttpMethod.Get);

        string? did = JsonData.SelectString(response, "did");

        Logger.LogTrace($"ResolveHandleToDid_ViaBlueskyApi: did: {did}");
        return did;
    }


    /// <summary>
    /// Resolves a did to didDoc, for did:plc.
    /// </summary>
    /// <param name="did">The did to resolve.</param>
    /// <returns>The resolved didDoc or null if not found.</returns>
    public static string? ResolveDidToDidDoc_DidPlc(string? did)
    {
        Logger.LogTrace($"ResolveDidToDidDoc_DidPlc: did: {did}");

        if (string.IsNullOrEmpty(did) || !did.StartsWith("did:plc"))
        {
            Logger.LogError($"ResolveDidToDidDoc_DidPlc: invalid did, exiting.");
            return null;
        }

        string? didDoc = null;
        string url = $"https://plc.directory/{did}";
        Logger.LogTrace($"ResolveDidToDidDoc_DidPlc: url: {url}");

        var response = BlueskyClient.SendRequest(url, HttpMethod.Get);

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
        Logger.LogTrace($"ResolveDidToDidDoc_DidWeb: did: {did}");

        if (string.IsNullOrEmpty(did) || !did.StartsWith("did:web:"))
        {
            Logger.LogError($"ResolveDidToDidDoc_DidWeb: invalid did, exiting.");
            return null;
        }

        string hostname = did.Replace("did:web:", "");
        string url = $"https://{hostname}/.well-known/did.json";
        Logger.LogTrace($"ResolveDidToDidDoc_DidWeb: url: {url}");

        var response = BlueskyClient.SendRequest(url, HttpMethod.Get);

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
        Logger.LogTrace($"ResolveDidDocToPds: didDoc length: {didDoc?.Length}");

        if (string.IsNullOrEmpty(didDoc))
        {
            Logger.LogError("DidDoc is null or empty.");
            return null;
        }

        JsonNode? didDocJson = JsonNode.Parse(didDoc);
        if (didDocJson == null) return null;

        foreach (var service in didDocJson["service"]?.AsArray() ?? new JsonArray())
        {
            if (service == null) continue;
            var serviceType = service["type"]?.ToString();
            if (serviceType == "AtprotoPersonalDataServer")
            {
                var pds = service["serviceEndpoint"]?.ToString();
                if (!string.IsNullOrEmpty(pds))
                {
                    return pds.Replace("https://", "").Replace("http://", "");
                }
            }
        }
        
        return null;
    }


    /// <summary>
    /// Gets the profile of an actor.
    /// https://docs.bsky.app/docs/api/app-bsky-actor-get-profile
    /// </summary>
    /// <param name="actor">The actor to get the profile for.</param>
    public static JsonNode? GetProfile(string? actor, string? accessJwt = null, string? hostname = null, string? labelers = null)
    {
        if (string.IsNullOrEmpty(hostname))
        {
            // we were calling "public.api.bsky.app", but the labels weren't returning
            // from that endpoint. Switching to "api.bsky.app" seems to work.
            hostname = "api.bsky.app";
        }
        
        Logger.LogTrace($"GetProfile: actor: {actor}");
        if (string.IsNullOrEmpty(actor))
        {
            Logger.LogError("GetProfile: Actor is null or empty. Exiting.");
            return null;
        }

        string url = $"https://{hostname}/xrpc/app.bsky.actor.getProfile?actor={actor}";
        Logger.LogTrace($"GetProfile: url: {url}");

        JsonNode? profile = BlueskyClient.SendRequest(url, HttpMethod.Get, accessJwt: accessJwt, labelers: labelers);

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
        Logger.LogTrace($"GetRepo: pds: {pds}, did: {did}, repoFile: {repoFile}");

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did) || string.IsNullOrEmpty(repoFile))
        {
            Logger.LogError("GetRepo: Invalid arguments. Exiting.");
            return;
        }

        string url = $"https://{pds}/xrpc/com.atproto.sync.getRepo?did={did}";
        Logger.LogTrace($"GetRepo: url: {url}");

        BlueskyClient.SendRequest(url,
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
        Logger.LogTrace($"ListBlobs: pds: {pds}, did: {did}");
        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did))
        {
            Logger.LogError("ListBlobs: Invalid arguments. Exiting.");
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

            Logger.LogTrace($"ListBlobs: url: {url}");

            JsonNode? response = BlueskyClient.SendRequest(url, HttpMethod.Get);

            var cids = response?["cids"]?.AsArray();

            keepGoing = cids != null && cids.Count == limit;

            cursor = response?["cursor"]?.ToString();

            if (cids != null)
            {
                Logger.LogTrace($"ListBlobs: Count: {cids.Count}");
                Logger.LogTrace($"ListBlobs: Cursor: {cursor}");

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
            Logger.LogTrace($"ListBlobs: Writing blobs to file: {blobsFile}");
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
        Logger.LogTrace($"GetBlob: pds: {pds}, did: {did}, cid: {cid}, blobFile: {blobFile}");

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did) || string.IsNullOrEmpty(cid) || string.IsNullOrEmpty(blobFile))
        {
            Logger.LogError("GetBlob: Invalid arguments. Exiting.");
            return;
        }

        string url = $"https://{pds}/xrpc/com.atproto.sync.getBlob?did={did}&cid={cid}";
        Logger.LogTrace($"GetBlob: url: {url}");

        BlueskyClient.SendRequest(url,
            HttpMethod.Get,
            outputFilePath: blobFile,
            parseJsonResponse: false);

    }


    /// <summary>
    /// CreateSession. Find the pds via ResolveHandleInfo, then call CreateSession with pds.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="password"></param>
    /// <param name="authFactorToken"></param>
    /// <returns></returns>
    public static JsonNode? CreateSession(string? handle, string? password, string? authFactorToken)
    {
        // first resolvehandleinfo, to get pds
        var handleInfo = BlueskyClient.ResolveActorInfo(handle);
        if (string.IsNullOrEmpty(handleInfo.Pds))
        {
            Logger.LogError("Could not resolve handle to pds.");
            return null;
        }

        return CreateSession(handleInfo.Pds, handle, password, authFactorToken);
    }


    /// <summary>
    /// Create a session for the user. This is "logging in". It returns a session but the important property is accessJwt.
    /// </summary>
    /// <param name="pds"></param>
    /// <param name="handle"></param>
    /// <param name="password"></param>
    /// <param name="authFactorToken"></param>
    /// <returns></returns>
    public static JsonNode? CreateSession(string? pds, string? handle, string? password, string? authFactorToken)
    {
        //
        // Construct url
        //
        string url = $"https://{pds}/xrpc/com.atproto.server.createSession";
        Logger.LogTrace($"url: {url}");


        //
        // Send request
        //
        JsonNode? session = BlueskyClient.SendRequest(url,
            HttpMethod.Post,
            content: string.IsNullOrEmpty(authFactorToken) ?
                new StringContent(JsonSerializer.Serialize(new
                {
                    identifier = handle,
                    password = password
                })) :
                new StringContent(JsonSerializer.Serialize(new
                {
                    identifier = handle,
                    password = password,
                    authFactorToken = authFactorToken
                }))
        );

        if (session == null)
        {
            Logger.LogError("Session returned null.");
            return null;
        }

        // add pds
        session["pds"] = pds;

        //
        // Process response
        //
        return session;
    }
    
    /// <summary>
    /// Deletes a record.
    /// </summary>
    /// <param name="handleInfo"></param>
    /// <param name="accessJwt"></param>
    /// <param name="postUri"></param>
    public static void DeleteRecord(string? pds, string? did, string? accessJwt, string? rkey, string? collection = "app.bsky.feed.post")
    {
        //
        // Check args
        //
        Logger.LogTrace($"DeleteRecord: pds: {pds}, did: {did}, collection: {collection}, rkey: {rkey}");

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did) || string.IsNullOrEmpty(accessJwt) || string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(rkey))
        {
            Logger.LogError("DeleteRecord: Invalid arguments. Exiting.");
            return;
        }

        //
        // Call pds to delete
        //
        string url = $"https://{pds}/xrpc/com.atproto.repo.deleteRecord";
        Logger.LogTrace($"DeleteRecord: url: {url}");

        var response = BlueskyClient.SendRequest(url,
            HttpMethod.Post,
            accessJwt: accessJwt,
            content: new StringContent(JsonSerializer.Serialize(new
            {
                repo = did,
                collection = collection,
                rkey = rkey
            }))
        );

        if (response == null)
        {
            Logger.LogError("DeletePost: response returned null.");
            return;
        }

        BlueskyClient.LogTraceJsonResponse(response);
    }


    /// <summary>
    /// Return set of bookmarks for the user.
    /// </summary>
    /// <param name="pds"></param>
    /// <param name="accessJwt"></param>
    /// <returns></returns>
    public static List<(string createdAt, AtUri uri)> GetBookmarks(string? pds, string? accessJwt)
    {
        List<(string createdAt, AtUri uri)> ret = new List<(string createdAt, AtUri uri)>();

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(accessJwt))
        {
            Logger.LogError("GetBookmarks: Invalid arguments. Exiting.");
            return ret;
        }

        bool keepGoing = true;
        string? cursor = null;
        while (keepGoing)
        {
            keepGoing = false;
            string? url = null;

            if (cursor != null)
            {
                url = $"https://{pds}/xrpc/app.bsky.bookmark.getBookmarks?cursor={cursor}";
            }
            else
            {
                url = $"https://{pds}/xrpc/app.bsky.bookmark.getBookmarks";
            }

            Logger.LogTrace($"GetBookmarks: url: {url}");

            var response = BlueskyClient.SendRequest(url,
                HttpMethod.Get,
                accessJwt: accessJwt
            );

            if (response == null)
            {
                Logger.LogError("GetBookmarks: response returned null.");
                return ret;
            }

            BlueskyClient.LogTraceJsonResponse(response);

            var bookmarks = response["bookmarks"]?.AsArray();
            if (bookmarks != null)
            {
                foreach (var bookmark in bookmarks)
                {
                    keepGoing = true;
                    var uri = bookmark?["item"]?["uri"]?.ToString();
                    var createdAt = bookmark?["item"]?["record"]?["createdAt"]?.ToString();

                    if (!string.IsNullOrEmpty(uri))
                    {
                        var atUri = AtUri.FromAtUri(uri);
                        if (atUri != null && !string.IsNullOrEmpty(createdAt))
                        {
                            ret.Add(((string)createdAt, atUri));
                        }
                    }
                }
            }

            cursor = response?["cursor"]?.ToString();

            if (keepGoing)
            {
                Thread.Sleep(1000);
            }
        }


        return ret;
    }


    /// <summary>
    /// Many calls to the Bluesky APIs follow the same pattern. This function implements that pattern.
    /// You'll see this being called in commands like "GetUnreadCount" and "ResolveHandle".
    /// If user specifies an output file, the response is written to that file.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="getOrPut"></param>
    /// <param name="accessJwt"></param>
    /// <param name="contentType"></param>
    /// <param name="content"></param>
    /// <param name="outputFilePath"></param>
    /// <returns></returns>
    public static JsonNode? SendRequest(string url, HttpMethod getOrPut, string? accessJwt = null, string contentType = "application/json", StringContent? content = null, bool parseJsonResponse = true, string? outputFilePath = null, string? acceptHeader = null, string? userAgent = "dnproto", string? labelers = null)
    {
        Logger.LogTrace($"SendRequest: {url}");

        using (HttpClient client = new HttpClient())
        {
            //
            // Set up request
            //
            var request = new HttpRequestMessage(getOrPut, url);

            if (content != null)
            {
                request.Content = content;
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }

            if (accessJwt != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);
            }

            if (!string.IsNullOrEmpty(acceptHeader))
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptHeader));
            }

            if (!string.IsNullOrEmpty(userAgent))
            {
                request.Headers.UserAgent.TryParseAdd(userAgent);
            }

            if (!string.IsNullOrEmpty(labelers))
            {
                request.Headers.Add("Atproto-Accept-Labelers", labelers);
            }

            Logger.LogTrace($"REQUEST: {request}");

            //
            // Send
            //
            var response = client.Send(request);

            if (response == null)
            {
                Logger.LogError("In SendRequest, response is null.");
                return null;
            }

            Logger.LogTrace($"RESPONSE: {response}");

            bool succeeded = response.StatusCode == HttpStatusCode.OK;

            if (!succeeded) 
            {
                Logger.LogError($"Request failed with status code: {response.StatusCode}");
                return null;
            }

            //
            // If user wants json, parse that.
            //
            JsonNode? jsonResponse = null;
            if (parseJsonResponse && succeeded)
            {
                using (var reader = new StreamReader(response.Content.ReadAsStream()))
                {
                    var responseText = reader.ReadToEnd();

                    if (string.IsNullOrEmpty(responseText) == false)
                    {
                        jsonResponse = JsonNode.Parse(responseText);
                    }
                }
            }

            //
            // If the user has specified an output file, write the response to that file.
            //
            if (string.IsNullOrEmpty(outputFilePath) == false && succeeded)
            {
                Logger.LogTrace($"writing to: {outputFilePath}");

                if (parseJsonResponse)
                {
                    JsonData.WriteJsonToFile(jsonResponse, outputFilePath);
                }
                else
                {
                    using (var responseStream = response.Content.ReadAsStream())
                    {
                        using (var fs = new FileStream(outputFilePath, FileMode.Create))
                        {
                            responseStream.CopyTo(fs);
                        }
                    }
                }
            }

            return jsonResponse;
        }
    }

    /// <summary>
    /// Same as above, but just get string.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="getOrPut"></param>
    /// <param name="accessJwt"></param>
    /// <param name="contentType"></param>
    /// <param name="content"></param>
    /// <param name="outputFilePath"></param>
    /// <returns></returns>
    public static string? SendRequestEx(string url, HttpMethod getOrPut, string? accessJwt = null, string contentType = "application/json", StringContent? content = null, string? outputFilePath = null, string? acceptHeader = null, string? userAgent = "dnproto")
    {
        Logger.LogTrace($"SendRequest: {url}");

        using (HttpClient client = new HttpClient())
        {
            //
            // Set up request
            //
            var request = new HttpRequestMessage(getOrPut, url);

            if (content != null)
            {
                request.Content = content;
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }


            if (accessJwt != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);
            }

            if (!string.IsNullOrEmpty(acceptHeader))
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptHeader));
            }

            if (!string.IsNullOrEmpty(userAgent))
            {
                request.Headers.UserAgent.TryParseAdd(userAgent);
            }

            Logger.LogTrace($"REQUEST:\n{request}");

            //
            // Send
            //
            var response = client.Send(request);

            if(response == null)
            {
                Logger.LogTrace("response is null.");
                return null;
            }

            Logger.LogTrace($"RESPONSE:\n{response}");


            //
            // Get response text.
            //
            string? responseText = null;
            using (var reader = new StreamReader(response.Content.ReadAsStream()))
            {
                responseText = reader.ReadToEnd();
            }
            
            //
            // If the user has specified an output file, write the response to that file.
            //
            if (string.IsNullOrEmpty(outputFilePath) == false && !string.IsNullOrEmpty(responseText))
            {
                Logger.LogTrace($"writing to: {outputFilePath}");
                File.WriteAllText(outputFilePath, responseText);
            }

            return responseText;
        }
    }

    /// <summary>
    /// Currently most of the commands just print the response to the console.
    /// </summary>
    /// <param name="response"></param>
    public static void PrintJsonResponseToConsole(JsonNode? response)
    {
        if(response == null)
        {
            Logger.LogError("response returned null.");
            return;
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        Logger.LogInfo(response.ToJsonString(options));
    }

    public static void LogTraceJsonResponse(JsonNode? response)
    {
        if(response == null)
        {
            Logger.LogError("response returned null.");
            return;
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        Logger.LogTrace(response.ToJsonString(options));
    }
}