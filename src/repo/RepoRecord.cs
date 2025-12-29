
namespace dnproto.repo;

/// <summary>
/// The record of a dag cbor file. Each item in a repo (post, like, follow, etc.) is stored as a record.
/// The DataBlock contains the data. You can view a pretty-printed version of the data in JsonString.
/// </summary>

public class RepoRecord
{
    public required CidV1 Cid { get; set; }

    public required DagCborObject DataBlock { get; set; }

    public string? JsonString { get; set; }

    /// <summary>
    /// The $type field in a AT Proto record (ex: "app.bsky.feed.post")
    /// </summary>
    public string? AtProtoType { get; set; }

    public string? CreatedAt { get; set; }

    public bool IsError { get; set; }

    public static RepoRecord ReadFromStream(Stream s)
    {

        //
        // The block length is the first VarInt in the stream.
        //
        VarInt blockLength = VarInt.ReadVarInt(s);


        //
        // Use the block length to read the entire block into a memory stream.
        //
        int l = (int)blockLength.Value;
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
        string? atProtoType = null;
        string? createdAt = null;
        bool isError = false;
        try
        {
            dataBlock = DagCborObject.ReadFromStream(ms, dataBlockDict);
            createdAt = dataBlock.SelectString(["createdAt"]);
        }
        catch (Exception ex)
        {
            isError = true;
            dataBlock = DagCborObject.FromException(ex, buffer, dataBlockDict);
        }

        atProtoType = dataBlock.SelectString(["$type"]);

        var recordJson = JsonData.ConvertObjectToJsonString(dataBlock.GetRawValue());

        //
        // Return
        //
        return new RepoRecord
        {
            Cid = cid,
            DataBlock = dataBlock,
            JsonString = recordJson,
            AtProtoType = atProtoType,
            CreatedAt = createdAt,
            IsError = isError
        };
    }
}

public class AtProtoType
{
    public static readonly string BLUESKY_FOLLOW = "app.bsky.graph.follow";

    public static readonly string BLUESKY_LIKE = "app.bsky.feed.like";

    public static readonly string BLUESKY_POST = "app.bsky.feed.post";

    public static readonly string BLUESKY_REPOST = "app.bsky.feed.repost";

    public static readonly string BLUESKY_BLOCK = "app.bsky.graph.block";

    public static readonly string FLASHES_POST = "blue.flashes.feed.post";

    public static readonly string VERIFICATION = "app.bsky.graph.verification";
}