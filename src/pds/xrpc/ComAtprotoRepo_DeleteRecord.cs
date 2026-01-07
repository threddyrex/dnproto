
using System.Text.Json.Nodes;
using dnproto.repo;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoRepo_DeleteRecord : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        //
        // Get the jwt from the caller's Authorization header
        //
        if(CheckUserAuth() == false)
        {
            Pds.Logger.LogInfo("ComAtprotoRepo_DeleteRecord: Unauthorized call to DeleteRecord.");
            return Results.Json(new { error = "InvalidRequest", message = "Need auth" }, statusCode: 204);
        }


        //
        // Get body input
        //
        JsonNode? requestBody = GetRequestBodyAsJson();
        string? rkey, collection;

        if(requestBody is null 
            || ! CheckRequestBodyParam(requestBody, "collection", out collection)
            || ! CheckRequestBodyParam(requestBody, "rkey", out rkey)
            || string.IsNullOrEmpty(collection)
            || string.IsNullOrEmpty(rkey)
        )
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: invalid params." }, statusCode: 400);
        }


        //
        // Call UserRepo to delete record
        //
        var (repoHeader, repoCommit) = Pds.UserRepo.DeleteRecord(collection, rkey);

        //
        // Return response
        //
        if(repoCommit is not null
            && repoCommit.Cid is not null
            && string.IsNullOrEmpty(repoCommit.Rev) == false
            )
        {
            var responseObj = new JsonObject
            {
                ["commit"] = new JsonObject
                {
                    ["cid"] = repoCommit.Cid.Base32,
                    ["rev"] = repoCommit.Rev
                }
            };

            return Results.Json(responseObj, statusCode: 200);
        }
        else
        {
            return Results.Json(new { error = "CreateRecordFailed", message = "Error creating record." }, statusCode: 400);
        }
    }
}