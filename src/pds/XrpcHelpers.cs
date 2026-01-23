

namespace dnproto.pds;

public static class XrpcHelpers
{
    public static string GetRequestBodyArgumentValue(string body, string key)
    {
        if(string.IsNullOrEmpty(body))
        {
            throw new InvalidOperationException("Request body is empty.");
        }

        var keyValuePairs = body.Split('&');
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