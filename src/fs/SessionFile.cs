
using dnproto.sdk.ws;
namespace dnproto.sdk.fs;


public class SessionFile
{
    public required ActorInfo ActorInfo { get; set; }

    public required string pds { get; set; }

    public required string did { get; set; }

    public required string accessJwt { get; set; }

    public required string refreshJwt { get; set; }

    public required string filePath { get; set; }
}