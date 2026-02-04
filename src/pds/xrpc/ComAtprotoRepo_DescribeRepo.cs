using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using dnproto.repo;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoRepo_DescribeRepo : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        IncrementStatistics();
        
        //
        // Get our did doc
        //
        string userDid = Pds.Config.UserDid;
        string userHandle = Pds.Config.UserHandle;
        ActorInfo? actorInfo = Pds.LocalFileSystem.ResolveActorInfo(userDid);
        string didDocString = actorInfo?.DidDoc ?? "{}";

        // Parse the did doc string to a JsonNode
        JsonNode? didDocNode = JsonNode.Parse(didDocString);

        // Get the collections
        List<string> collections = Pds.PdsDb.GetUniqueCollections();

        var response = new DescribeRepoResponse
        {
            Handle = userHandle,
            Did = userDid,
            DidDoc = didDocNode,
            Collections = collections,
            HandleIsCorrect = true
        };

        return Results.Json(response, contentType: "application/json");
    }
}

public class DescribeRepoResponse
{
    [JsonPropertyName("handle")]
    public string Handle { get; set; } = string.Empty;

    [JsonPropertyName("did")]
    public string Did { get; set; } = string.Empty;

    [JsonPropertyName("didDoc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? DidDoc { get; set; }

    [JsonPropertyName("collections")]
    public List<string> Collections { get; set; } = new List<string>();

    [JsonPropertyName("handleIsCorrect")]
    public bool HandleIsCorrect { get; set; }
}