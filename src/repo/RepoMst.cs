

using dnproto.mst;

namespace dnproto.repo;


public class RepoMst
{
    public static Mst LoadMstFromRepo(string repoFile)
    {
        using(var fs = new FileStream(repoFile, FileMode.Open, FileAccess.Read))
        {
            return LoadMstFromRepo(fs);
        }
    }

    public static Mst LoadMstFromRepo(Stream s)
    {
        List<MstItem> mstItems = new List<MstItem>();

        //
        // Walk repo and get mst nodes
        //
        Repo.WalkRepo(s,
            (header) =>
            {
                return true;
            },
            (record) =>
            {
                if(RepoMst.IsMstNode(record))
                {
                    // Entries
                    var entriesObj = (List<DagCborObject>?)record.DataBlock.SelectObjectValue(new []{"e"});
                    if (entriesObj != null)
                    {
                        List<string> fullKeys = new List<string>();
                        List<CidV1> recordCids = new List<CidV1>();

                        for(int i = 0; i < entriesObj.Count; i++)
                        {
                            // "p" - prefix length
                            int prefixLength = entriesObj[i].SelectInt(new[] { "p" }) ?? 0;

                            // "k" - key suffix
                            var keyBytes = (byte[]?)entriesObj[i].SelectObjectValue(new[] { "k" });
                            string? keySuffix = keyBytes != null ? System.Text.Encoding.UTF8.GetString(keyBytes) : null;

                            // "v" - record CID
                            CidV1? cid = (CidV1?)entriesObj[i].SelectObjectValue(new[] { "v" });

                            if(cid is null || keySuffix is null)
                            {
                                throw new Exception("CID or key suffix is null");
                            }

                            string fullKey = (i == 0) ? keySuffix : fullKeys[i-1].Substring(0, prefixLength) + keySuffix;
                            fullKeys.Add(fullKey);
                            recordCids.Add(cid);

                            mstItems.Add(new MstItem() { Key = fullKey, Value = cid.Base32 });
                        }
                    }
                }

                return true;
            });
        

        //
        // Make mst
        //
        Mst mst = Mst.AssembleTreeFromItems(mstItems);

        //
        // Return
        //
        return mst;
    }




    public static (CidV1, DagCborObject) ConvertMstNodeToDagCbor(Dictionary<MstNode, (CidV1, DagCborObject)> cache, MstNode node)
    {
        //
        // If not cached, create it.
        //
        if(cache.ContainsKey(node) == false)
        {

            //
            // Create empty dict
            //
            var nodeDict = new Dictionary<string, DagCborObject>();


            //
            // Add left link if present
            //
            if (node.LeftTree != null)
            {
                if(!cache.ContainsKey(node.LeftTree))
                {
                    ConvertMstNodeToDagCbor(cache, node.LeftTree);
                }

                (CidV1 leftCid, DagCborObject leftObj) = cache[node.LeftTree];

                nodeDict["l"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                    Value = leftCid
                };
            }
            else
            {
                nodeDict["l"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_SIMPLE_VALUE, AdditionalInfo = 0x16, OriginalByte = 0 },
                    Value = "null"
                };
            }


            //
            // Add entries array
            //
            var entriesArray = new List<DagCborObject>();
            List<int> prefixLengths = GetPrefixLengths(node.Entries);
            List<string> keySuffixes = GetKeySuffixes(node.Entries);
            for(int i = 0; i < node.Entries.Count; i++)
            {
                var entry = node.Entries[i];
                var entryDict = new Dictionary<string, DagCborObject>();

                // "p" - prefix length
                entryDict["p"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_UNSIGNED_INT, AdditionalInfo = 0, OriginalByte = 0 },
                    Value = prefixLengths[i]
                };

                // "k" - key suffix (byte string)
                entryDict["k"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_BYTE_STRING, AdditionalInfo = 0, OriginalByte = 0 },
                    Value = System.Text.Encoding.UTF8.GetBytes(keySuffixes[i])
                };

                // "v" - value CID
                entryDict["v"] = new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                    Value = CidV1.FromBase32(entry.Value)
                };

                // "t" - tree CID (nullable)
                if (entry.RightTree != null)
                {
                    if(!cache.ContainsKey(entry.RightTree))
                    {
                        ConvertMstNodeToDagCbor(cache, entry.RightTree);
                    }

                    (CidV1 rightCid, DagCborObject rightObj) = cache[entry.RightTree];

                    entryDict["t"] = new DagCborObject
                    {
                        Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                        Value = rightCid
                    };
                }
                else
                {
                    entryDict["t"] = new DagCborObject
                    {
                        Type = new DagCborType { MajorType = DagCborType.TYPE_SIMPLE_VALUE, AdditionalInfo = 0x16, OriginalByte = 0 },
                        Value = "null"
                    };
                }

                entriesArray.Add(new DagCborObject
                {
                    Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
                    Value = entryDict
                });
            }

            nodeDict["e"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_ARRAY, AdditionalInfo = 0, OriginalByte = 0 },
                Value = entriesArray
            };

            //
            // Make enclosing MAP object.
            //
            var nodeObj = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_MAP, AdditionalInfo = 0, OriginalByte = 0 },
                Value = nodeDict
            };


            //
            // Write to cache (for other callers).
            //
            cache[node] = (CidV1.ComputeCidForDagCbor(nodeObj)!, nodeObj);

        }

        
        //
        // Return
        //
        return cache[node];
    }




    public static bool IsMstNode(RepoRecord record)
    {
        bool notNull = record.DataBlock != null;
        bool isMap = record.DataBlock?.Type.MajorType == DagCborType.TYPE_MAP;
        bool containsE = (record.DataBlock?.SelectObjectValue(new[]{"e"}) as List<DagCborObject>) != null;
        return notNull && isMap && containsE;        
    }

    public static List<int> GetPrefixLengths(List<MstEntry> entries)
    {
        var prefixLengths = new List<int>();
        string previousFullKey = string.Empty;
        for (int i = 0; i < entries.Count; i++)
        {
            if (i == 0)
            {
                prefixLengths.Add(0);
                previousFullKey = entries[i].Key;
            }
            else
            {
                int prefixLen = GetCommonPrefixLength(previousFullKey, entries[i].Key);
                prefixLengths.Add(prefixLen);
                previousFullKey = entries[i].Key;
            }
        }
        return prefixLengths;
    }

    public static List<string> GetKeySuffixes(List<MstEntry> entries)
    {
        var keySuffixes = new List<string>();
        string previousFullKey = string.Empty;
        for (int i = 0; i < entries.Count; i++)
        {
            if (i == 0)
            {
                keySuffixes.Add(entries[i].Key);
                previousFullKey = entries[i].Key;
            }
            else
            {
                int prefixLen = GetCommonPrefixLength(previousFullKey, entries[i].Key);
                keySuffixes.Add(entries[i].Key.Substring(prefixLen));
                previousFullKey = entries[i].Key;
            }
        }
        return keySuffixes;
    }

    
    
    /// <summary>
    /// Get the length of the common prefix between two keys.
    /// </summary>
    public static int GetCommonPrefixLength(string a, string b)
    {
        int len = 0;
        int minLen = Math.Min(a.Length, b.Length);
        
        for (int i = 0; i < minLen; i++)
        {
            if (a[i] == b[i])
            {
                len++;
            }
            else
            {
                break;
            }
        }
        
        return len;
    }

}