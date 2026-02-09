

using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoRepo_GetRecord : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        IncrementStatistics();
        
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
            repo = Pds.PdsDb.GetConfigProperty("UserDid");
        }

        //
        // Check if this is a request for a different repo - if so, proxy it
        //
        bool isLocalRepo = repo == Pds.PdsDb.GetConfigProperty("UserDid") || repo == Pds.PdsDb.GetConfigProperty("UserHandle");
        
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
        bool recordExists = Pds.PdsDb.RecordExists(collection!, rkey!);
        if(!recordExists)
        {
            return Results.Json(new { error = "NotFound", message = "Error: Record not found." }, statusCode: 404);
        }

        string uri = $"at://{Pds.PdsDb.GetConfigProperty("UserDid")}/{collection}/{rkey}";

        RepoRecord repoRecord = Pds.PdsDb.GetRepoRecord(collection!, rkey!);



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