

namespace dnproto.pds;


public class Config
{
    public static string Version = "dnproto 0.0.004";    
    
    public required string ListenScheme;

    public required string ListenHost;

    public required int ListenPort;
    
    public required string PdsDid;

    public required string PdsHostname;

    public required string AvailableUserDomain;

    public required string AdminHashedPassword;

    public required string JwtSecret;

    public required string UserHandle;

    public required string UserDid;

    public required string UserHashedPassword;

    public required string UserEmail;

    public required string UserPublicKeyMultibase;

    public required string UserPrivateKeyMultibase;

    public required bool UserIsActive;
}