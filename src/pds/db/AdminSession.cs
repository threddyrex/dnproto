

namespace dnproto.pds;


public class AdminSession
{
    public required string SessionId { get; set; }

    public required string IpAddress { get; set; }

    public required string UserAgent { get; set; }

    public required string CreatedDate { get; set; }

    public required string AuthType { get; set; }
}