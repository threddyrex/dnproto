
using System.Text.Json.Nodes;
using dnproto.repo;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoRepo_CreateRecord : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        //
        // Require auth
        //
        if(UserIsFullyAuthorized() == false)
        {
            var (response, statusCode) = GetAuthFailureResponse();
            return Results.Json(response, statusCode: statusCode);
        }



        //
        // Get body input
        //
        JsonNode? requestBody = GetRequestBodyAsJson();
        string? repo, collection, rkey = null;
        DagCborObject? record;

        if(requestBody is null 
            || ! CheckRequestBodyParam(requestBody, "collection", out collection)
            || ! CheckRequestBodyParam(requestBody, "repo", out repo)
            || ! CheckRequestBodyParam(requestBody, "record", out record)
            || string.IsNullOrEmpty(collection)
            || string.IsNullOrEmpty(repo)
            || record is null
        )
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: invalid params." }, statusCode: 400);
        }

        // rkey is optional
        CheckRequestBodyParam(requestBody, "rkey", out rkey);

        if(rkey is null)
        {
            rkey = RecordKey.GenerateTid();
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
        // Call UserRepo to create record
        //
        UserRepo.ApplyWritesResult result = Pds.UserRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = collection,
                Rkey = rkey,
                Record = record
            }
        }).First();


        //
        // Get the new stuff
        //
        RepoRecord? repoRecord = Pds.UserRepo.GetRecord(collection, rkey);
        RepoCommit? repoCommit = Pds.PdsDb.GetRepoCommit();
        string? validationStatus = result.ValidationStatus;
        string? uri = result.Uri;

        //
        // Return response
        //
        if(string.IsNullOrEmpty(uri) == false 
            && repoRecord is not null
            && repoRecord.Cid is not null 
            && repoCommit is not null
            && repoCommit.Cid is not null
            && string.IsNullOrEmpty(validationStatus) == false
            && string.IsNullOrEmpty(repoCommit.Rev) == false
            )
        {
            var responseObj = new JsonObject
            {
                ["uri"] = uri,
                ["cid"] = repoRecord.Cid.Base32,
                ["commit"] = new JsonObject
                {
                    ["cid"] = repoCommit.Cid.Base32,
                    ["rev"] = repoCommit.Rev
                },
                ["validationStatus"] = validationStatus
            };

            return Results.Json(responseObj, statusCode: 200);
        }
        else
        {
            return Results.Json(new { error = "CreateRecordFailed", message = "Error creating record." }, statusCode: 400);
        }
    }
}