
using dnproto.utils;
namespace dnproto.repo;

/// <summary>
/// Repo Format
///
/// Format from spec:
/// 
///    [---  header  --------]   [----------------- data ---------------------------------]
///    [varint | header block]   [varint | cid | data block]....[varint | cid | data block] 
///
/// 
/// represented using the data types we have:
/// 
///    [---  header  --------]   [----------------- data ---------------------------------]
///    [VarInt | CborObject  ]   [VarInt | Cid | CborObject]....[VarInt | Cid | CborObject] 
///
/// 
/// https://ipld.io/specs/transport/car/carv1/#format-description
/// 
/// </summary>

public class RepoRecord
{
    public required VarInt Length { get; set; }

    public required CidV1 Cid { get; set; }

    public required DagCborObject DataBlock { get; set; }

    public string? JsonString { get; set; }

    public string? RecordType { get; set; }

    public string? CreatedAt { get; set; }

    public static RepoRecord ReadFromStream(Stream s)
    {
        VarInt blockLength = VarInt.ReadVarInt(s);
        CidV1 cid = CidV1.ReadCid(s);
        var dataBlock = DagCborObject.ReadFromStream(s);

        var recordJson = JsonData.GetObjectJsonString(dataBlock.GetRawValue());
        var recordType = dataBlock.SelectString(["$type"]);
        var createdAt = dataBlock.SelectString(["createdAt"]);

        return new RepoRecord
        {
            Length = blockLength,
            Cid = cid,
            DataBlock = dataBlock,
            JsonString = recordJson,
            RecordType = recordType,
            CreatedAt = createdAt
        };
    }
}