

namespace dnproto.pds;


public class OauthRequest
{
    public required string RequestUri { get; set; }
    public required string ExpiresDate { get; set; }
    public required string Dpop { get; set; }
    public required string Body { get; set; }


    public string GetRequestBodyArgumentValue(string key)
    {
        if(string.IsNullOrEmpty(Body))
        {
            throw new InvalidOperationException("Request body is empty.");
        }

        var keyValuePairs = Body.Split('&');
        foreach(var kvp in keyValuePairs)
        {
            var parts = kvp.Split('=');
            if(parts.Length == 2 && parts[0] == key)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        throw new InvalidOperationException($"Key '{key}' not found in request body.");
    }
}