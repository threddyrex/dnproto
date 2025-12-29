

using dnproto.repo;

namespace dnproto.pds.db;

/// <summary>
/// Record that contains atproto data
/// </summary>
public class DbRepoRecord
{
    /// <summary>
    /// Cid for this record.
    /// Base 32, starting with "b".
    /// </summary>
    public CidV1? Cid { get; set; }

    /// <summary>
    /// Data for this record.
    /// </summary>
    public DagCborObject? DagCborObject { get; set; }

}