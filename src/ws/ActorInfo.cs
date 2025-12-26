using System.Text.Json;
using System.Text.Json.Nodes;

using dnproto.repo;

namespace dnproto.ws;

public class ActorInfo
{
    public string? Actor { get; set; }

    public string? Handle { get; set; }

    public string? Did { get; set; }

    public string? Did_Bsky { get; set; }

    public string? Did_Http { get; set; }

    public string? Did_Dns { get; set; }

    public string? DidDoc { get; set; }

    public string? Pds { get; set; }

    public string? PublicKeyMultibase { get; set; }

    public string? ToJsonString()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(this, options);
    }

    public static ActorInfo? FromJsonString(string json)
    {
        if(string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<ActorInfo>(json);    
    }
}