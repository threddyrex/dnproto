

using dnproto.pds.xrpc;

namespace dnproto.pds;


public class OauthRequest
{
    public required string RequestUri { get; set; }

    public required string ExpiresDate { get; set; }

    public required string Dpop { get; set; }

    public required string Body { get; set; }
    
    public string? AuthorizationCode { get; set; } = null;

    public string? AuthType { get; set; } = null;

    public string? GetCodeChallenge()
    {
        return XrpcHelpers.GetRequestBodyArgumentValue(Body, "code_challenge");
    }
}