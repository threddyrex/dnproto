

using dnproto.repo;

namespace dnproto.pds.db;

/// <summary>
/// Entry point for the PDS repo.
/// </summary>
public class DbRepoHeader
{
    /// <summary>
    /// Points to the cid of the root commit for the repo.
    /// Base 32, starting with "b".
    /// </summary>
    public CidV1? RepoCommitCid { get; set; } = null;

    /// <summary>
    /// Version. Always 1 for now.
    /// </summary>
    public required int Version { get; set; } = 1;

}