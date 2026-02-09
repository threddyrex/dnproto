using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;

public class ComAtprotoSync_GetRepoStatus : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        IncrementStatistics();
        
        
        var ret = new JsonObject()
        {
            ["did"] = Pds.PdsDb.GetConfigProperty("PdsDid"),
            ["active"] = true
        };
        // add response header of "application/json"
        return Results.Json(ret, contentType: "application/json");
    }
}
