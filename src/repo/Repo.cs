

namespace dnproto.repo;


public class Repo
{

    /// <summary>
    /// 
    /// Start at beginning and read through entire repo. 
    /// Use callbacks to let caller know when we find things.
    /// This is just the top-level algorithm. Most of the heavy lifting
    /// is done in VarInt, CidV1, and DagCborObject.
    /// 
    /// https://ipld.io/specs/transport/car/carv1/
    /// https://atproto.com/specs/repository
    /// https://ipld.io/specs/codecs/dag-cbor/spec/
    /// 
    ///
    /// Format from spec:
    /// 
    ///    [---  header  -------- ]   [----------------- data ---------------------------------]
    ///    [varint | header block ]   [varint | cid | data block]....[varint | cid | data block] 
    ///
    /// 
    /// Represented using the data types we have:
    /// 
    ///    [---  header  -------- ]   [----------------- data -------------------------------------------]
    ///    [VarInt | DagCborObject]   [VarInt | CidV1 | DagCborObject]....[VarInt | CidV1 | DagCborObject] 
    ///
    ///
    /// </summary>
    /// <param name="s"></param>
    /// <param name="headerCallback"></param>   
    /// <param name="recordCallback"></param>
    public static void WalkRepo(Stream s, Func<RepoHeader, bool> headerCallback, Func<RepoRecord, bool> recordCallback)
    {
        if (s == null) return;
        if (s.Length == 0) return;

        // Read header
        var repoHeader = RepoHeader.ReadFromStream(s);
        bool keepGoing = headerCallback(repoHeader);

        while (s.Position < s.Length && keepGoing)
        {
            // Read data block (record)
            var repoRecord = RepoRecord.ReadFromStream(s);
            keepGoing = recordCallback(repoRecord);
        }
    }


    public static void WalkRepo(string repoFile, Func<RepoHeader, bool> headerCallback, Func<RepoRecord, bool> recordCallback)
    {
        if (string.IsNullOrEmpty(repoFile)) return;
        using (var fs = new FileStream(repoFile, FileMode.Open))
        {
            WalkRepo(fs, headerCallback, recordCallback);
        }
    }
}