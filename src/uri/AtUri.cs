using System.Text.Json;
using System.Text.Json.Nodes;

namespace dnproto.uri;

/// <summary>
/// Represents an AT URI
/// spec: https://atproto.com/specs/at-uri-scheme
/// </summary>
public class AtUri
{
    // The authority (did or handle) of the user
    public string? Authority { get; set; }

    // The collection type (ex: "app.bsky.feed.post")
    public string? Collection { get; set; }

    // The rkey id
    public string? Rkey { get; set; }

    /// <summary>
    /// Parse a Bsky post url and construct AtUri object
    /// </summary>
    public static AtUri? FromBskyPost(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        var uri = new AtUri();
        uri.Collection = "app.bsky.feed.post";

        string[] urlParts = url?.Split('/') ?? Array.Empty<string>();
        uri.Authority = urlParts.Length > 4 ? urlParts[4] : null;
        uri.Rkey = urlParts.Length > 6 ? urlParts[6] : null;

        return uri;
    }

    /// <summary>
    /// Parse an AT URI and construct AtUri object
    /// </summary>
    public static AtUri? FromAtUri(string? atUri)
    {
        if (string.IsNullOrEmpty(atUri))
        {
            return null;
        }

        var uri = new AtUri();
        string[] parts = atUri.Split('/');
        if (parts.Length < 3)
        {
            return null;
        }

        uri.Authority = parts[2];
        uri.Collection = parts.Length > 3 ? parts[3] : null;
        uri.Rkey = parts.Length > 4 ? parts[4] : null;

        return uri;
    }


    public string ToAtUri()
    {
        return $"at://{Authority}/{Collection}/{Rkey}";
    }

    public string ToBskyPostUri()
    {
        return $"https://bsky.app/profile/{Authority}/post/{Rkey}";
    }

    public string ToDebugString()
    {
        return $"AtUri -> Authority: {Authority}, Collection: {Collection}, Rkey: {Rkey}";
    }
}
