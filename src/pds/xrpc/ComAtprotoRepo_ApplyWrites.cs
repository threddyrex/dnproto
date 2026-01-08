
using System.Text.Json.Nodes;
using dnproto.repo;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoRepo_ApplyWrites : BaseXrpcCommand
{
    private const string TypeCreate = "com.atproto.repo.applyWrites#create";
    private const string TypeUpdate = "com.atproto.repo.applyWrites#update";
    private const string TypeDelete = "com.atproto.repo.applyWrites#delete";

    public IResult GetResponse()
    {
        //
        // Get the jwt from the caller's Authorization header
        //
        if(CheckUserAuth() == false)
        {
            Pds.Logger.LogInfo("ComAtprotoRepo_ApplyWrites: Unauthorized call to ApplyWrites.");
            return Results.Json(new { error = "InvalidRequest", message = "Need auth" }, statusCode: 204);
        }


        //
        // Get body input
        //
        JsonNode? requestBody = GetRequestBodyAsJson();
        string? repo;

        if(requestBody is null 
            || !CheckRequestBodyParam(requestBody, "repo", out repo)
            || string.IsNullOrEmpty(repo)
        )
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: invalid params." }, statusCode: 400);
        }


        //
        // Get writes array
        //
        JsonArray? writes = requestBody["writes"]?.AsArray();
        if(writes is null || writes.Count == 0)
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: writes array is required." }, statusCode: 400);
        }


        //
        // Optional: swapCommit check
        //
        string? swapCommit = requestBody["swapCommit"]?.ToString();
        if(!string.IsNullOrEmpty(swapCommit))
        {
            var currentCommit = Pds.PdsDb.GetRepoCommit();
            if(currentCommit?.Cid?.Base32 != swapCommit)
            {
                return Results.Json(new { error = "InvalidSwap", message = "Commit CID mismatch." }, statusCode: 400);
            }
        }


        //
        // Process each write operation
        //
        JsonArray results = new JsonArray();
        RepoCommit? lastRepoCommit = null;

        foreach(JsonNode? writeNode in writes)
        {
            if(writeNode is null)
            {
                return Results.Json(new { error = "InvalidRequest", message = "Error: null write operation." }, statusCode: 400);
            }

            string? type = writeNode["$type"]?.ToString();
            string? collection = writeNode["collection"]?.ToString();
            string? rkey = writeNode["rkey"]?.ToString();

            if(string.IsNullOrEmpty(type) || string.IsNullOrEmpty(collection))
            {
                return Results.Json(new { error = "InvalidRequest", message = "Error: missing $type or collection in write operation." }, statusCode: 400);
            }

            switch(type)
            {
                case TypeCreate:
                    {
                        var result = ProcessCreate(collection, rkey, writeNode, out lastRepoCommit);
                        if(result is null)
                        {
                            return Results.Json(new { error = "ApplyWritesFailed", message = "Error processing create operation." }, statusCode: 400);
                        }
                        results.Add(result);
                        break;
                    }

                case TypeUpdate:
                    {
                        if(string.IsNullOrEmpty(rkey))
                        {
                            return Results.Json(new { error = "InvalidRequest", message = "Error: rkey is required for update operation." }, statusCode: 400);
                        }
                        var result = ProcessUpdate(collection, rkey, writeNode, out lastRepoCommit);
                        if(result is null)
                        {
                            return Results.Json(new { error = "ApplyWritesFailed", message = "Error processing update operation." }, statusCode: 400);
                        }
                        results.Add(result);
                        break;
                    }

                case TypeDelete:
                    {
                        if(string.IsNullOrEmpty(rkey))
                        {
                            return Results.Json(new { error = "InvalidRequest", message = "Error: rkey is required for delete operation." }, statusCode: 400);
                        }
                        var result = ProcessDelete(collection, rkey, out lastRepoCommit);
                        if(result is null)
                        {
                            return Results.Json(new { error = "ApplyWritesFailed", message = "Error processing delete operation." }, statusCode: 400);
                        }
                        results.Add(result);
                        break;
                    }

                default:
                    return Results.Json(new { error = "InvalidRequest", message = $"Error: unknown write operation type: {type}" }, statusCode: 400);
            }
        }


        //
        // Return response
        //
        if(lastRepoCommit is not null 
            && lastRepoCommit.Cid is not null 
            && !string.IsNullOrEmpty(lastRepoCommit.Rev))
        {
            var responseObj = new JsonObject
            {
                ["commit"] = new JsonObject
                {
                    ["cid"] = lastRepoCommit.Cid.Base32,
                    ["rev"] = lastRepoCommit.Rev
                },
                ["results"] = results
            };

            return Results.Json(responseObj, statusCode: 200);
        }
        else
        {
            return Results.Json(new { error = "ApplyWritesFailed", message = "Error applying writes." }, statusCode: 400);
        }
    }


    /// <summary>
    /// Process a create operation.
    /// </summary>
    private JsonObject? ProcessCreate(string collection, string? rkey, JsonNode writeNode, out RepoCommit? repoCommit)
    {
        repoCommit = null;

        string? valueStr = writeNode["value"]?.ToString();
        if(string.IsNullOrEmpty(valueStr))
        {
            return null;
        }

        DagCborObject? record = DagCborObject.FromJsonString(valueStr);
        if(record is null)
        {
            return null;
        }

        var (uri, repoRecord, commit, validationStatus) = Pds.UserRepo.CreateRecord(collection, record, rkey);
        repoCommit = commit;

        if(string.IsNullOrEmpty(uri) || repoRecord?.Cid is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["$type"] = "com.atproto.repo.applyWrites#createResult",
            ["uri"] = uri,
            ["cid"] = repoRecord.Cid.Base32,
            ["validationStatus"] = validationStatus
        };
    }


    /// <summary>
    /// Process an update operation.
    /// </summary>
    private JsonObject? ProcessUpdate(string collection, string rkey, JsonNode writeNode, out RepoCommit? repoCommit)
    {
        repoCommit = null;

        string? valueStr = writeNode["value"]?.ToString();
        if(string.IsNullOrEmpty(valueStr))
        {
            return null;
        }

        DagCborObject? record = DagCborObject.FromJsonString(valueStr);
        if(record is null)
        {
            return null;
        }

        var (uri, repoRecord, commit, validationStatus) = Pds.UserRepo.PutRecord(collection, rkey, record);
        repoCommit = commit;

        if(string.IsNullOrEmpty(uri) || repoRecord?.Cid is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["$type"] = "com.atproto.repo.applyWrites#updateResult",
            ["uri"] = uri,
            ["cid"] = repoRecord.Cid.Base32,
            ["validationStatus"] = validationStatus
        };
    }


    /// <summary>
    /// Process a delete operation.
    /// </summary>
    private JsonObject? ProcessDelete(string collection, string rkey, out RepoCommit? repoCommit)
    {
        var (repoHeader, commit) = Pds.UserRepo.DeleteRecord(collection, rkey);
        repoCommit = commit;

        if(commit is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["$type"] = "com.atproto.repo.applyWrites#deleteResult"
        };
    }
}