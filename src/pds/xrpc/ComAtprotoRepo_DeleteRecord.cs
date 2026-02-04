
using System.Text.Json.Nodes;
using dnproto.repo;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoRepo_DeleteRecord : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        IncrementStatistics();
        
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
        Pds.UserRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Delete,
                Collection = collection,
                Rkey = rkey
            }
        }
        , GetCallerIpAddress(), GetCallerUserAgent());
    
        //
        // Get the new stuff
        //
        RepoCommit? repoCommit = Pds.PdsDb.GetRepoCommit();

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