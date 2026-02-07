
namespace dnproto.pds;


public class OauthSession
{
    public required string SessionId { get; set; }

    public required string ClientId { get; set; }

    public required string Scope { get; set; }

    public required string DpopJwkThumbprint { get; set; }

    public required string RefreshToken { get; set; }

    public required string RefreshTokenExpiresDate { get; set; }

    public required string CreatedDate { get; set; }

    public required string IpAddress { get; set; }

    public required string AuthType { get; set; }
}
