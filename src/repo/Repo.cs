
using System.Text;
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



    /// <summary>
    /// 
    /// Find the rkeys in a repo file. Return it as a cid->rkey dictionary.
    /// 
    /// https://atproto.com/specs/repository#repo-data-structure-v3
    /// 
    /// </summary>
    /// <param name="repoFile"></param>
    /// <returns></returns>
    public static Dictionary<string, string>? FindRkeys(string repoFile)
    {
        if (string.IsNullOrEmpty(repoFile)) return null;

        Dictionary<string, string> rkeys = new Dictionary<string, string>();

        Repo.WalkRepo(
            repoFile,
            (header) => { return true; },
            (repoRecord) =>
            {
                if (repoRecord == null) return true;

                // merkle records have "e" as the root item
                List<DagCborObject>? e = repoRecord.DataBlock.SelectObject(["e"]) as List<DagCborObject>;
                if (e == null) return true;

                // loop through the items
                string? kCurrent = null;
                foreach (DagCborObject node in e)
                {
                    string? v = node.SelectString(["v"]);

                    object? kobj = node.SelectObject(["k"]);
                    if (kobj == null) break;
                    byte[]? kbytes = kobj as byte[];
                    if (kbytes == null) break;
                    string? k = Encoding.UTF8.GetString(kbytes);

                    int? p = node.SelectInt(["p"]);

                    if (string.IsNullOrEmpty(v)) break;
                    if (string.IsNullOrEmpty(k)) break;
                    if (p == null) break;

                    if (p == 0)
                    {
                        kCurrent = k;
                    }
                    else if (kCurrent != null)
                    {
                        kCurrent = kCurrent.Substring(0, (int)p) + k;
                    }

                    if (string.IsNullOrEmpty(kCurrent) == false) rkeys[v] = kCurrent.Split("/").Last();
                }

                return true;
            }
        );

        return rkeys;
    }

    /// <summary>
    /// Read through the repo blocks and find the record 
    /// that specifies this repo's did.
    /// </summary>
    /// <param name="repoFile"></param>
    /// <returns></returns>
    public static string? FindDid(string? repoFile)
    {
        if (string.IsNullOrEmpty(repoFile)) return null;

        string? ret = null;

        //
        // walk
        //
        Repo.WalkRepo(
            repoFile,

            (header) => { return true; },

            (repoRecord) =>
            {
                if (repoRecord == null) return true;

                string? did = repoRecord.DataBlock.SelectString(["did"]);
                string? rev = repoRecord.DataBlock.SelectString(["rev"]);
                string? data = repoRecord.DataBlock.SelectString(["data"]);
                string? version = repoRecord.DataBlock.SelectString(["version"]);

                if (string.IsNullOrEmpty(did) == false
                    && string.IsNullOrEmpty(rev) == false
                    && string.IsNullOrEmpty(data) == false
                    && string.IsNullOrEmpty(version) == false)
                {
                    ret = did;
                    return false;
                }

                return true;
            }
        );

        return ret;
    }
}