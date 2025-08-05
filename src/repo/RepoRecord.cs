
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

        //
        // The block length is the first VarInt in the stream.
        //
        VarInt blockLength = VarInt.ReadVarInt(s);


        //
        // Use the block length to read the entire block into a memory stream.
        //
        int l = blockLength.Value;
        byte[] buffer = new byte[l];
        int bytesRead = s.Read(buffer, 0, l);
        if (bytesRead != l)
        {
            throw new Exception($"Failed to read {l} bytes from stream. Read {bytesRead} bytes.");
        }

        using MemoryStream ms = new MemoryStream(buffer);


        //
        // Read the record from the memory stream.
        //
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

        //
        // Return
        //
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