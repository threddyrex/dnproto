using System.Text.Json;
using System.Text.Json.Serialization;

namespace dnproto.pds;

public class Xrpc_Health
{
    public static string GetResponse()
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
