using System.Text.Json;
using System.Text.Json.Serialization;

namespace dnproto.pds.xrpc;

public class Health : BaseXrpcCommand
{
    public override string GetResponse()
    {
        var health = new HealthResponse
        {
            Version = "0.0.001"
        };
        
        return JsonSerializer.Serialize(health);
    }
}

public class HealthResponse
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}
