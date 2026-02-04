

namespace dnproto.pds;

public class LegacySession
{
    public required string CreatedDate { get; set; }
    public required string AccessJwt { get; set; }
    public required string RefreshJwt { get; set; }
    public required string IpAddress { get; set; }
    public required string UserAgent { get; set; }
}