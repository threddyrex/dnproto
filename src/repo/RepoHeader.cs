
namespace dnproto.repo;

/// <summary>
/// The header for a dag cbor file.
/// </summary>
public class RepoHeader
{
    /// <summary>
    /// Points to the cid of the root commit for the repo.
    /// Base 32, starting with "b".
    /// </summary>
    public required CidV1 RepoCommitCid;

    /// <summary>
    /// Version. Always 1 for now.
    /// </summary>
    public required int Version;


    #region STREAM

    public static RepoHeader ReadFromStream(Stream s)
    {
        var headerLength = VarInt.ReadVarInt(s);
        var header = DagCborObject.ReadFromStream(s);
        return FromDagCborObject(header);
    }

    public void WriteToStream(Stream s)
    {
        var headerDagCbor = this.ToDagCborObject();
        var headerDagCborBytes = headerDagCbor.ToBytes();
        var headerLengthVarInt = VarInt.FromLong((long)headerDagCborBytes.Length);
        VarInt.WriteVarInt(s, headerLengthVarInt);
        s.Write(headerDagCborBytes, 0, headerDagCborBytes.Length);
    }

    #endregion


    #region DAG-CBOR

    public static RepoHeader FromDagCborObject(DagCborObject dagCborObject)
    {
        CidV1? repoCommitCid = null;
        int? version = null;


        var headerJson = JsonData.ConvertObjectToJsonString(dagCborObject.GetRawValue());
        var headerDict = (Dictionary<string, DagCborObject>?) dagCborObject.Value;

        if (headerDict != null && headerDict.ContainsKey("roots"))
        {
            var rootsArray = (List<DagCborObject>?) headerDict["roots"].Value;
            if (rootsArray?.Count > 0)
            {
                var firstRoot = rootsArray[0];
                var cid = (CidV1?) firstRoot.Value;
                repoCommitCid = cid;
            }
        }

        if (headerDict != null && headerDict.ContainsKey("version"))
        {
            var versionValue = (int) headerDict["version"].Value;
            version = versionValue;
        }

        if (repoCommitCid == null || version == null)
        {
            throw new Exception("Invalid RepoHeader.");
        }

        var repoHeader = new RepoHeader
        {
            RepoCommitCid = repoCommitCid,
            Version = version.Value
        };

        return repoHeader;
    }



    public byte[] ToDagCborBytes()
    {
        var dagCborObject = ToDagCborObject();
        if (dagCborObject == null)
            throw new Exception("Invalid RepoHeader.");

        using var ms = new MemoryStream();
        DagCborObject.WriteToStream(dagCborObject, ms);
        return ms.ToArray();
    }

    public DagCborObject ToDagCborObject()
    {
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

    #endregion
}