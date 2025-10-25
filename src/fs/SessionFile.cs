public class SessionFile
{
    public required HandleInfo HandleInfo { get; set; }

    public required string pds { get; set; }

    public required string did { get; set; }

    public required string accessJwt { get; set; }

    public required string refreshJwt { get; set; }

    public required string filePath { get; set; }
}