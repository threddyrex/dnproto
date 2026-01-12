

using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoRepo_ListRecords : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        //
        // Get param
        //
        string? collection = HttpContext.Request.Query.ContainsKey("collection") ? (string?) HttpContext.Request.Query["collection"] : null;
        if(collection == null)
        {
            return Results.Json(new { error = "InvalidRequest", message = "Error: Param 'collection' is required." }, statusCode: 400);
        }

        string? cursor = HttpContext.Request.Query.ContainsKey("cursor") ? (string?) HttpContext.Request.Query["cursor"] : null;
        string? limitStr = HttpContext.Request.Query.ContainsKey("limit") ? (string?) HttpContext.Request.Query["limit"] : null;
        int limit = 100;
        if(limitStr != null)
        {
            int.TryParse(limitStr, out limit);
        }



        //
        // Retrieve record
        //
        List<(string rkey, RepoRecord)> records = Pds.PdsDb.ListRepoRecordsByCollection(collection!, limit, cursor);



        //
        // Return value
        //
        var returnRecords = new JsonArray();

        foreach(var r in records)
        {
            returnRecords.Add(new JsonObject
            {
                ["uri"] = $"at://{Pds.Config.UserDid}/{collection}/{r.rkey}",
                ["cid"] = r.Item2.Cid.Base32,
                ["value"] = r.Item2.JsonString != null ? JsonNode.Parse(r.Item2.JsonString)! : null
            });
        }

        return Results.Json(new JsonObject
        {
            ["records"] = returnRecords,  
        }, statusCode: 200);
    }
}