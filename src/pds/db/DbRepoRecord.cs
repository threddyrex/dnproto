

namespace dnproto.pds.db;

/// <summary>
/// Record that contains atproto data
/// </summary>
public class RepoRecord
{
    /// <summary>
    /// Cid for this record.
    /// Base 32, starting with "b".
    /// </summary>
    public required string Cid { get; set; }

    /// <summary>
    /// Json data for this record.
    /// Storing as json string for now, for debugging and readability.
    /// I might regret the performance impacts later.
    /// </summary>
    public required string JsonData { get; set; }

}