using dnproto.repo;

public class HandleInfo
{
    public string? Query { get; set; }

    public string? Handle { get; set; }

    public string? Did { get; set; }

    public string? Did_Bsky { get; set; }

    public string? Did_Http { get; set; }

    public string? Did_Dns { get; set; }

    public string? DidDoc { get; set; }

    public string? Pds { get; set; }

    public string? ToJsonString()
    {
        return JsonData.ConvertObjectToJsonString(this);
    }

    public static HandleInfo? FromJsonString(string json)
    {
        return JsonData.ConvertJsonStringToObject(json) as HandleInfo;
    }
}