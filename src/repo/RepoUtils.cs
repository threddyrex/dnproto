
using System.Text;
namespace dnproto.repo;


public class RepoUtils
{
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
                List<DagCborObject>? e = repoRecord.DataBlock.SelectObjectValue(["e"]) as List<DagCborObject>;
                if (e == null) return true;

                // loop through the items
                string? kCurrent = null;
                foreach (DagCborObject node in e)
                {
                    string? v = node.SelectString(["v"]);

                    object? kobj = node.SelectObjectValue(["k"]);
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