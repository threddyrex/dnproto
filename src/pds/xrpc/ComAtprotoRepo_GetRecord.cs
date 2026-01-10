

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
        // Get param
        //
        string? collection = HttpContext.Request.Query.ContainsKey("collection") ? (string?) HttpContext.Request.Query["collection"] : null;
        string? rkey = HttpContext.Request.Query.ContainsKey("rkey") ? (string?) HttpContext.Request.Query["rkey"] : null;

        if(string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(rkey))
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: Params must have 'collection' and 'rkey'." }, statusCode: 400);
        }


        //
        // Retrieve record
        //
        bool recordExists = Pds.UserRepo.RecordExists(collection!, rkey!);
        if(!recordExists)
        {
            return Results.Json(new { error = "NotFound", message = "Error: Record not found." }, statusCode: 404);
        }

        string uri = $"at://{Pds.Config.UserDid}/{collection}/{rkey}";

        RepoRecord repoRecord = Pds.UserRepo.GetRecord(collection!, rkey!);

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