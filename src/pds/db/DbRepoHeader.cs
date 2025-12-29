

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


    public byte[]? ToDagCborBytes()
    {
        var dagCborObject = ToDagCborObject();
        if (dagCborObject == null)
            return null;

        using var ms = new MemoryStream();
        DagCborObject.WriteToStream(dagCborObject, ms);
        return ms.ToArray();
    }

    public DagCborObject? ToDagCborObject()
    {
        //
        // Validate
        //
        if (RepoCommitCid == null)
            return null;

        //
        // Create header object
        //
        var headerDict = new Dictionary<string, DagCborObject>();

        var rootsArray = new List<DagCborObject>
        {
            new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = RepoCommitCid
            }
        };

        headerDict["roots"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_ARRAY, AdditionalInfo = 0, OriginalByte = 0 },
            Value = rootsArray
        };

        headerDict["version"] = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 0, OriginalByte = 0 },
            Value = Version
        };

        var headerObj = new DagCborObject
        {
            Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
            Value = headerDict
        };


        //
        // Return
        //
        return headerObj;
    }

}