
using System.Text.Json.Nodes;
using dnproto.repo;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoRepo_ApplyWrites : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        //
        // Require auth
        //
        if(UserIsAuthenticated() == false)
        {
            var (response, statusCode) = GetAuthenticationFailureResponse();
            return Results.Json(response, statusCode: statusCode);
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
        JsonArray? writesArray = requestBody["writes"]?.AsArray();
        if(writesArray is null || writesArray.Count == 0)
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
        // Parse write operations
        //
        List<UserRepo.ApplyWritesOperation> writes = new List<UserRepo.ApplyWritesOperation>();

        foreach(JsonNode? writeNode in writesArray)
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

            // Generate rkey if not provided for create operations
            if(string.IsNullOrEmpty(rkey))
            {
                if(type == UserRepo.ApplyWritesType.Create)
                {
                    rkey = RecordKey.GenerateTid();
                }
                else
                {
                    return Results.Json(new { error = "InvalidRequest", message = "Error: rkey is required for update/delete operations." }, statusCode: 400);
                }
            }

            // Parse record for create/update operations
            DagCborObject? record = null;
            if(type == UserRepo.ApplyWritesType.Create || type == UserRepo.ApplyWritesType.Update)
            {
                string? valueStr = writeNode["value"]?.ToString();
                if(string.IsNullOrEmpty(valueStr))
                {
                    return Results.Json(new { error = "InvalidRequest", message = "Error: value is required for create/update operations." }, statusCode: 400);
                }

                record = DagCborObject.FromJsonString(valueStr);
                if(record is null)
                {
                    return Results.Json(new { error = "InvalidRequest", message = "Error: failed to parse record value." }, statusCode: 400);
                }
            }

            writes.Add(new UserRepo.ApplyWritesOperation
            {
                Type = type,
                Collection = collection,
                Rkey = rkey,
                Record = record
            });
        }


        //
        // Apply writes using UserRepo.ApplyWrites
        //
        List<UserRepo.ApplyWritesResult> results = Pds.UserRepo.ApplyWrites(writes);

        if(results.Count == 0)
        {
            return Results.Json(new { error = "ApplyWritesFailed", message = "Error applying writes." }, statusCode: 400);
        }


        //
        // Build response
        //
        var repoCommit = Pds.PdsDb.GetRepoCommit();
        if(repoCommit is null || repoCommit.Cid is null || string.IsNullOrEmpty(repoCommit.Rev))
        {
            return Results.Json(new { error = "ApplyWritesFailed", message = "Error applying writes." }, statusCode: 400);
        }

        JsonArray resultsArray = new JsonArray();
        foreach(var result in results)
        {
            var resultObj = new JsonObject
            {
                ["$type"] = result.Type
            };

            if(result.Uri is not null)
            {
                resultObj["uri"] = result.Uri;
            }

            if(result.Cid is not null)
            {
                resultObj["cid"] = result.Cid.Base32;
            }

            if(result.ValidationStatus is not null)
            {
                resultObj["validationStatus"] = result.ValidationStatus;
            }

            resultsArray.Add(resultObj);
        }

        var responseObj = new JsonObject
        {
            ["commit"] = new JsonObject
            {
                ["cid"] = repoCommit.Cid.Base32,
                ["rev"] = repoCommit.Rev
            },
            ["results"] = resultsArray
        };

        return Results.Json(responseObj, statusCode: 200);
    }
}