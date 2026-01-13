

using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoRepo_GetRecord : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        //
        // Get params
        //
        string? repo = HttpContext.Request.Query.ContainsKey("repo") ? (string?) HttpContext.Request.Query["repo"] : null;
        string? collection = HttpContext.Request.Query.ContainsKey("collection") ? (string?) HttpContext.Request.Query["collection"] : null;
        string? rkey = HttpContext.Request.Query.ContainsKey("rkey") ? (string?) HttpContext.Request.Query["rkey"] : null;

        if(string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(rkey))
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: Params must have 'collection' and 'rkey'." }, statusCode: 400);
        }

        // Default to local user if repo not specified
        if (string.IsNullOrEmpty(repo))
        {
            repo = Pds.Config.UserDid;
        }

        //
        // Check if this is a request for a different repo - if so, proxy it
        //
        bool isLocalRepo = repo == Pds.Config.UserDid || repo == Pds.Config.UserHandle;
        
        if (!isLocalRepo)
        {
            Pds.Logger.LogInfo($"Proxying getRecord request for repo: {repo}");
            
            // Resolve the repo (could be DID or handle) to find their PDS
            var actorInfo = Pds.LocalFileSystem.ResolveActorInfo(repo);
            
            if (actorInfo == null || string.IsNullOrEmpty(actorInfo.Pds) || string.IsNullOrEmpty(actorInfo.Did))
            {
                Pds.Logger.LogError($"Unable to resolve actor info for repo: {repo}");
                return Results.Json(new { error = "NotFound", message = "Unable to resolve repository" }, statusCode: 404);
            }

            // Proxy the request to the target PDS
            string targetUrl = $"https://{actorInfo.Pds}/xrpc/com.atproto.repo.getRecord?repo={actorInfo.Did}&collection={collection}&rkey={rkey}";
            Pds.Logger.LogInfo($"Proxying to: {targetUrl}");

            try
            {
                JsonNode? response = BlueskyClient.SendRequest(targetUrl, System.Net.Http.HttpMethod.Get);
                
                if (response == null)
                {
                    return Results.Json(new { error = "NotFound", message = "Record not found" }, statusCode: 404);
                }

                return Results.Json(response, statusCode: 200);
            }
            catch (Exception ex)
            {
                Pds.Logger.LogError($"Error proxying getRecord request: {ex.Message}");
                return Results.Json(new { error = "NotFound", message = "Record not found" }, statusCode: 404);
            }
        }

        //
        // Retrieve local record
        //
        bool recordExists = Pds.UserRepo.RecordExists(collection!, rkey!);
        if(!recordExists)
        {
            return Results.Json(new { error = "NotFound", message = "Error: Record not found." }, statusCode: 404);
        }

        string uri = $"at://{Pds.Config.UserDid}/{collection}/{rkey}";

        RepoRecord repoRecord = Pds.UserRepo.GetRecord(collection!, rkey!);


        //
        // Fix up profile record if needed
        //
        if (string.Equals("app.bsky.actor.profile", collection))
        {
            DagCborObject? avatarRef = repoRecord.DataBlock.SelectObject(new string[] { "avatar", "ref" });

            if (avatarRef != null)
            {
                if(avatarRef.Type.MajorType == DagCborType.TYPE_TEXT)
                {
                    try
                    {
                        CidV1 cid = CidV1.FromBase32((string)avatarRef.Value);
                        avatarRef.Value = new Dictionary<string, DagCborObject>
                        {
                            ["$link"] = new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT }, Value = cid.ToString() }
                        };
                    }
                    catch (Exception ex)
                    {
                        Pds.Logger.LogError($"Error converting avatar ref to CidV1: {ex.Message}");
                    }
                }
                // Convert from string/cid to a map with $link
                else if (avatarRef.Value is CidV1 cidValue)
                {
                    avatarRef.Value = new Dictionary<string, DagCborObject>
                    {
                        ["$link"] = new DagCborObject { Type = new DagCborType { MajorType = DagCborType.TYPE_TEXT }, Value = cidValue.ToString() }
                    };
                }
            }
        }


        //
        // Return success
        //
        return Results.Json(new JsonObject
        {
            ["uri"] = uri,
            ["cid"] = repoRecord.Cid.Base32,
            ["value"] = repoRecord.JsonString != null ? JsonNode.Parse(repoRecord.JsonString)! : null

        }, statusCode: 200);
    }
}