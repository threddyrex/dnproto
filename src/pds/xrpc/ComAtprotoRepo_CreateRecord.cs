using System.Security.Claims;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.log;
using dnproto.repo;
using dnproto.pds;
using dnproto.pds.db;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoRepo_CreateRecord : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        //
        // Get the jwt from the caller's Authorization header
        //
        if(CheckUserAuth() == false)
        {
            Pds.Logger.LogInfo("ComAtprotoRepo_CreateRecord: Unauthorized call to CreateRecord.");
            return Results.Json(new { error = "InvalidRequest", message = "Need auth" }, statusCode: 204);
        }


        //
        // Get body input
        //
        JsonNode? requestBody = GetRequestBodyAsJson();
        string? repo, collection;
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


        //
        // Call UserRepo to create record
        //
        var (uri, repoRecord, repoCommit, validationStatus) = Pds.UserRepo.CreateRecord(collection, record);


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