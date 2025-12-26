
namespace dnproto.repo;

/// <summary>
/// The header for a dag cbor file.
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
        var headerJson = JsonData.ConvertObjectToJsonString(header.GetRawValue());

        return new RepoHeader
        {
            Length = headerLength,
            Header = header,
            JsonString = headerJson
        };
    }
}