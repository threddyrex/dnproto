

using dnproto.repo;

namespace dnproto.pds.db;

/// <summary>
/// Repo Commit for the repo. Can be only one.
/// </summary>
public class DbRepoCommit
{
    public required int Version { get; set; } = 3;

    /// <summary>
    /// Cid of this commit object.
    /// Base 32, starting with "b".
    /// </summary>
    public CidV1? Cid { get; set; }

    /// <summary>
    /// Points to the cid of the root MST node for the repo.
    /// Base 32, starting with "b".
    /// </summary>
    public CidV1? RootMstNodeCid { get; set; }

    /// <summary>
    /// Revision string for this commit.
    /// Increases monotonically. Typically a timestamp-based string.
    /// </summary>
    public string? Rev { get; set; } = null;

    /// <summary>
    /// Points to the cid of the previous commit for the repo.
    /// Base 32, starting with "b".
    /// Usually null.
    /// </summary>
    public CidV1? PrevMstNodeCid { get; set; }

    /// <summary>
    /// Signature of this commit.
    /// Base 64.
    /// </summary>
    public string? Signature { get; set; }
}