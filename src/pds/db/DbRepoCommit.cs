

namespace dnproto.pds.db;

/// <summary>
/// Repo Commit for the repo. Can be only one.
/// </summary>
public class RepoCommit
{
    public required int Version { get; set; } = 3;

    /// <summary>
    /// Cid of this commit object.
    /// Base 32, starting with "b".
    /// </summary>
    public required string Cid { get; set; }

    /// <summary>
    /// Points to the cid of the root MST node for the repo.
    /// Base 32, starting with "b".
    /// </summary>
    public required string RootMstNodeCid { get; set; }

    /// <summary>
    /// Revision string for this commit.
    /// Increases monotonically. Typically a timestamp-based string.
    /// </summary>
    public required string Rev { get; set; }

    /// <summary>
    /// Points to the cid of the previous commit for the repo.
    /// Base 32, starting with "b".
    /// Usually null.
    /// </summary>
    public string? PrevMstNodeCid { get; set; }

    /// <summary>
    /// Signature of this commit.
    /// Base 64.
    /// </summary>
    public required string Signature { get; set; }
}