


using dnproto.fs;
using dnproto.log;


namespace dnproto.pds;


public interface IBlobDb
{
    public void InsertBlobBytes(string cid, byte[] bytes);

    public bool HasBlobBytes(string cid);

    public byte[] GetBlobBytes(string cid);

    public void DeleteBlobBytes(string cid);

    public void UpdateBlobBytes(string cid, byte[] bytes);
}



public class BlobDb : IBlobDb
{
    private LocalFileSystem Lfs;

    private IDnProtoLogger Logger;

    public static IBlobDb Create(LocalFileSystem lfs, IDnProtoLogger logger)
    {
        return new BlobDb(lfs, logger);
    }

    private BlobDb(LocalFileSystem lfs, IDnProtoLogger logger)
    {
        Lfs = lfs;
        Logger = logger;
    }

    private string GetBlobFilePath(string cid)
    {
        return Path.Combine(Lfs.GetDataDir(), "pds", "blobs", GetSafeString(cid));
    }

    private string GetSafeString(string input)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '_');
        }
        return input;
    }


    public void InsertBlobBytes(string cid, byte[] bytes)
    {
        string filePath = GetBlobFilePath(cid);
        File.WriteAllBytes(filePath, bytes);
    }

    public bool HasBlobBytes(string cid)
    {
        string filePath = GetBlobFilePath(cid);
        return File.Exists(filePath);
    }

    public byte[] GetBlobBytes(string cid)
    {
        string filePath = GetBlobFilePath(cid);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Blob not found: {cid}");
        }
        return File.ReadAllBytes(filePath);
    }

    public void DeleteBlobBytes(string cid)
    {
        string filePath = GetBlobFilePath(cid);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    public void UpdateBlobBytes(string cid, byte[] bytes)
    {
        string filePath = GetBlobFilePath(cid);
        File.WriteAllBytes(filePath, bytes);
    }
}