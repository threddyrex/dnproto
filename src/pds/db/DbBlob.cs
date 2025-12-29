namespace dnproto.pds.db;


public class DbBlob
{
    public string Cid { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public int ContentLength { get; set; } = 0;

    public byte[] Bytes { get; set; } = Array.Empty<byte>();
}
