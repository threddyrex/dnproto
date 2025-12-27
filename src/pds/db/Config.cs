

namespace dnproto.pds.db;


public class Config
{
    public string Version { get; set; } = "0.0.003";
    
    public string ListenScheme { get; set; } = "http";

    public string ListenHost { get; set; } = "localhost";

    public int ListenPort { get; set; } = 5001;

    public string PdsDid { get; set; } = string.Empty;

    public string PdsHostname { get; set; } = string.Empty;

    public string AvailableUserDomain { get; set; } = string.Empty;

    public string AdminHashedPassword { get; set; } = string.Empty;

    public string JwtSecret { get; set; } = string.Empty;

    public string UserHandle { get; set; } = string.Empty;

    public string UserDid { get; set; } = string.Empty;

    public string UserHashedPassword { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public string UserPublicKeyMultibase { get; set; } = string.Empty;

    public string UserPrivateKeyMultibase { get; set; } = string.Empty;
}