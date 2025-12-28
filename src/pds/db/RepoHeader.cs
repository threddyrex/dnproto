

namespace dnproto.pds.db;

public class RepoHeader
{
    public required string RepoCommitCid { get; set; }

    public required int Version { get; set; } = 1;

}