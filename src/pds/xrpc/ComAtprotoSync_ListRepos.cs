

using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoSync_ListRepos : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        IncrementStatistics();
        
        //
        // Return {logs:[]}
        //
        var repoCommit = Pds.PdsDb.GetRepoCommit();
        var repos = new JsonArray();
        repos.Add(new JsonObject
        {
            ["did"] = Pds.Config.UserDid,
            ["head"] = repoCommit?.Cid?.Base32,
            ["rev"] = repoCommit?.Rev,
            ["active"] = Pds.Config.UserIsActive
        });

        return Results.Json(new JsonObject
        {
            ["repos"] = repos
        }, statusCode: 200);
    }
}