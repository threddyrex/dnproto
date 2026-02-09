

namespace dnproto.pds;


public class Config
{
    // PDS
    public required string PdsDid;

    public required string PdsHostname;

    public required string AvailableUserDomain;

    public required string JwtSecret;



    // User
    public required string UserHandle;

    public required string UserDid;

    public required string UserHashedPassword;

    public required string UserEmail;

    public required string UserPublicKeyMultibase;

    public required string UserPrivateKeyMultibase;

    public required bool UserIsActive;


}