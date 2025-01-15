
using dnproto.utils;

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