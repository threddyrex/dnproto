using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;

public class Health : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        var health = new HealthResponse
        {
            Version = Config.Version
        };
        
        return Results.Json(health, contentType: "application/json");
    }
}

public class HealthResponse
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}
