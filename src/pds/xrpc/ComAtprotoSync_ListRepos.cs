

using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ChatBskyConvo_ListRepos : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        //
        // Return {logs:[]}
        //
        var repoCommit = Pds.PdsDb.GetRepoCommit();
        var repos = new JsonArray();
        repos.Add(new JsonObject
        {
            ["did"] = Pds.Config.UserDid,
            ["repo"] = repoCommit?.Cid?.Base32,
            ["rev"] = repoCommit?.Rev
        });

        return Results.Json(new JsonObject
        {
            ["repos"] = repos
        }, statusCode: 200);
    }
}