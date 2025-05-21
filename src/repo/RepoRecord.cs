
using dnproto.utils;
namespace dnproto.repo;

/// <summary>
/// The record of a dag cbor file. Each item in a repo (post, like, follow, etc.) is stored as a record.
/// The DataBlock contains the data. You can view a pretty-printed version of the data in JsonString.
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

        // Get length, and use that to read bytes into a memory stream.
        // Useful for debugging - we can just skip the record (storing bytes) and move to next record.
        int l = blockLength.Value;

        // Read l bytes into memory stream
        byte[] buffer = new byte[l];
        int bytesRead = s.Read(buffer, 0, l);
        if (bytesRead != l)
        {
            throw new Exception($"Failed to read {l} bytes from stream. Read {bytesRead} bytes.");
        }

        // Create a memory stream from the buffer
        using MemoryStream ms = new MemoryStream(buffer);

        // Read from memory stream instead of the original stream
        CidV1 cid = CidV1.ReadCid(ms);

        DagCborObject? dataBlock = null;
        Dictionary<string, DagCborObject>? dataBlockDict = new Dictionary<string, DagCborObject>();
        string? recordType = null;
        string? createdAt = null;
        try
        {
            dataBlock = DagCborObject.ReadFromStream(ms, dataBlockDict);
            createdAt = dataBlock.SelectString(["createdAt"]);
        }
        catch (Exception ex)
        {
            dataBlock = DagCborObject.FromException(ex, buffer, dataBlockDict);
        }

        recordType = dataBlock.SelectString(["$type"]);

        var recordJson = JsonData.ConvertObjectToJsonString(dataBlock.GetRawValue());

        // Return
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