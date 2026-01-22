

namespace dnproto.pds;


public class OauthRequest
{
    public required string RequestUri { get; set; }
    public required string ExpiresDate { get; set; }
    public required string Dpop { get; set; }
    public required string Body { get; set; }
}