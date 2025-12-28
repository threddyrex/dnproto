
public class AtProtoProxy
{
    public string Did { get; set; } = string.Empty;

    public string ServiceId { get; set; } = string.Empty;

    public static AtProtoProxy? FromHeader(string headerValue)
    {
        if(string.IsNullOrEmpty(headerValue))
        {
            return null;
        }

        string[] parts = headerValue.Split('#');

        if(parts.Length != 2)
        {
            return null;
        }

        if(parts[0].StartsWith("did:") == false)
        {
            return null;
        }

        return new AtProtoProxy
        {
            Did = parts[0],
            ServiceId = parts[1]
        };
    }
}