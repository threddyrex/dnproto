
using dnproto.utils;

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

public class RepoHeader
{
    public required VarInt Length { get; set; }

    public required DagCborObject Header { get; set; }

    public string? JsonString { get; set; }

    public static RepoHeader ReadFromStream(Stream s)
    {
        var headerLength = VarInt.ReadVarInt(s);
        var header = DagCborObject.ReadFromStream(s);
        var headerJson = JsonData.GetObjectJsonString(header.GetRawValue());

        return new RepoHeader
        {
            Length = headerLength,
            Header = header,
            JsonString = headerJson
        };
    }
}