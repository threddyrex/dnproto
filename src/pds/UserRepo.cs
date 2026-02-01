
using dnproto.fs;
using dnproto.log;
using dnproto.mst;
using dnproto.repo;
using System.Text.Json.Nodes;

namespace dnproto.pds;


/*
    Structure of repo and MST.
    The three Repo* classes are here in the "dnproto.repo" namespace. 
    The two MST classes are in the "dnproto.mst" namespace. 
    The "dnproto.mst" namespace is somewhat generic, so RepoMst.cs exists to bridge that gap.

    A repo will have:

        RepoHeader.cs (only one)
            CidV1 RepoCommitCid (points to RepoCommit.cs)
            Int Version

        RepoCommit.cs (only one)
            int Version (always 3)
            CidV1 Cid;
            CidV1 RootMstNodeCid (points to root MST node cid);
            string Rev (increases monotonically, typically timestamp)
            CidV1? PrevMstNodeCid (usually null)
            string Signature

        MstNode (0 or more)
            CidV1 Cid
            "l" - CidV1? LeftMstNodeCid (optional to a sub-tree node)

        MstEntry (0 or more)
            CidV1 MstNodeCid
            int EntryIndex (0-based index within parent MstNode)
            "k" - string KeySuffix
            "p" - int PrefixLength
            "t" - CidV1? TreeMstNodeCid
            "v" - CidV1 RecordCid (cid of atproto record)

        RepoRecord (0 or more)
            CidV1 Cid
            DagCborObject Data (the actual atproto record)
*/



/// <summary>
/// Managing the user's repository.
/// </summary>
public class UserRepo
{
    private LocalFileSystem _lfs;

    private IDnProtoLogger _logger;

    private PdsDb _db;

    private string _userDid;

    private Func<byte[], byte[]> _commitSigningFunction;
    
    private UserRepo(LocalFileSystem lfs, IDnProtoLogger logger, PdsDb db, Func<byte[], byte[]> commitSigningFunction, string userDid)
    {
        _lfs = lfs;
        _logger = logger;
        _db = db;
        _commitSigningFunction = commitSigningFunction;
        _userDid = userDid;
    }

    public static UserRepo ConnectUserRepo(LocalFileSystem lfs, IDnProtoLogger logger, PdsDb db, Func<byte[], byte[]> commitSigningFunction, string userDid)
    {
        return new UserRepo(lfs, logger, db, commitSigningFunction, userDid);
    }




    #region APPLYWRITES



    /// <summary>
    /// Main entry point for making any changes to the repo (create, update, delete).
    /// 
    /// This method updates the following:
    /// 
    ///     1. Repo Record (RepoRecord)
    ///     2. Repo Commit (RepoCommit)
    ///     3. Repo Header (RepoHeader)
    ///     4. Firehose (FirehoseEvent)
    /// 
    /// </summary>
    /// <param name="writes"></param>
    /// <returns></returns>
    public List<ApplyWritesResult> ApplyWrites(List<ApplyWritesOperation> writes, string? ip, string? userAgent)
    {
        //
        // The caller probably parsed this from json request.
        // If so, blob refs will need to be corrected.
        // Caught this by adding trace logging to the firehose consumer,
        // so that it prints out the types of DagCbor objects (and not just
        // the JSON representation)
        //
        FixBlobRefs(writes);

        DateTime startTime = DateTime.UtcNow;

        Pds.GLOBAL_PDS_LOCK.Wait();
        try
        {
            List<ApplyWritesResult> results = new List<ApplyWritesResult>();


            //
            // FIREHOSE: some state
            //
            RepoCommit before_repoCommit = _db.GetRepoCommit();
            RepoHeader before_repoHeader = _db.GetRepoHeader();
            JsonArray firehoseState_Ops = new JsonArray();

            //
            // Loop through operations and do writes.
            //
            foreach(var write in writes)
            {
                string uri = $"at://{_userDid}/{write.Collection}/{write.Rkey}";
                string fullKey = $"{write.Collection}/{write.Rkey}";

                _logger.LogInfo($"[REPO] ip={ip} type={write.Type} collection={write.Collection} rkey={write.Rkey} userAgent=\"{userAgent}\"");

                switch(write.Type)
                {
                    //
                    // CREATE/UPDATE
                    //
                    case ApplyWritesType.Create:
                    case ApplyWritesType.Update:

                        if(write.Record is null)
                        {
                            _logger.LogError($"[REPO] Update operation missing record for collection: {write.Collection} with rkey: {write.Rkey}");
                            continue;
                        }


                        //
                        // REPO RECORD
                        //
                        write.Record.SetString(new string[] { "$type" }, write.Collection);
                        CidV1 recordCid = CidV1.ComputeCidForDagCbor(write.Record)!;

                        if(write.Type == ApplyWritesType.Update && _db.RecordExists(write.Collection, write.Rkey))
                        {
                            _db.DeleteRepoRecord(write.Collection, write.Rkey);
                        }

                        _db.InsertRepoRecord(write.Collection, write.Rkey, recordCid, write.Record);



                        //
                        // Add to return list
                        //
                        string resultType = write.Type == ApplyWritesType.Create ? ApplyWritesType.CreateResult : ApplyWritesType.UpdateResult;
                        results.Add(new ApplyWritesResult
                        {
                            Type = resultType,
                            Uri = uri,
                            Cid = recordCid,
                            ValidationStatus = "valid"
                        });


                        //
                        // FIREHOSE: add operation to state
                        //
                        firehoseState_Ops.Add(new JsonObject()
                        {
                            ["cid"] = recordCid.ToString(),
                            ["path"] = fullKey,
                            ["action"] = write.Type == ApplyWritesType.Create ? "create" : "update"
                        });

                        break;
                    

                    //
                    // DELETE
                    //
                    case ApplyWritesType.Delete:

                        if(! _db.RecordExists(write.Collection, write.Rkey))
                        {
                            _logger.LogWarning($"[REPO] Delete operation skipped: record does not exist for collection: {write.Collection} with rkey: {write.Rkey}");
                            break;
                        }

                        //
                        // REPO RECORD
                        //
                        RepoRecord originalRepoRecord = _db.GetRepoRecord(write.Collection, write.Rkey);
                        CidV1 originalRepoRecordCid = originalRepoRecord.Cid;
                        _db.DeleteRepoRecord(write.Collection, write.Rkey);


                        //
                        // Add to return list
                        //
                        results.Add(new ApplyWritesResult
                        {
                            Type = ApplyWritesType.DeleteResult,
                            Uri = uri,
                            Cid = null
                        });


                        //
                        // FIREHOSE: add operation to state
                        //
                        firehoseState_Ops.Add(new JsonObject()
                        {
                            ["cid"] = "null",
                            ["path"] = $"{write.Collection}/{write.Rkey}",
                            ["prev"] = originalRepoRecordCid.ToString(),
                            ["action"] = "delete",
                        });

                        break;
                }
            }


            //
            // Find the nodes that we need to send back
            //
            Mst mst = Mst.AssembleTreeFromItems(_db.GetAllRepoRecordMstItems());
            HashSet<MstNode> nodesToSend = new HashSet<MstNode>();
            Dictionary<MstNode, (CidV1, DagCborObject)> mstNodeCache = new Dictionary<MstNode, (CidV1, DagCborObject)>();

            foreach(var write in writes)
            {
                string fullKey = $"{write.Collection}/{write.Rkey}";
                List<MstNode> nodes = mst.FindNodesForKey(fullKey);
                foreach(var node in nodes)
                {
                    nodesToSend.Add(node);
                    RepoMst.ConvertMstNodeToDagCbor(mstNodeCache, node);
                }
            }




            //
            // REPO COMMIT
            //
            CidV1 newRootMstNodeCid = mstNodeCache[mst.Root].Item1;
            var repoCommit = _db.GetRepoCommit()!;
            repoCommit.SignAndRecomputeCid(newRootMstNodeCid, _commitSigningFunction!);
            _db.InsertUpdateRepoCommit(repoCommit);


            //
            // REPO HEADER
            //
            var repoHeader = _db.GetRepoHeader()!;
            repoHeader.RepoCommitCid = repoCommit.Cid!;
            _db.InsertUpdateRepoHeader(repoHeader);

            
            //
            // FIREHOSE: OBJECT 1 (header)
            //
            int header_op = 1;
            string header_t = "#commit";
            var object1Json = new JsonObject()
            {
                ["t"] = header_t,
                ["op"] = header_op
            };

            DagCborObject object1DagCbor = DagCborObject.FromJsonString(object1Json.ToString());

            

            //
            // FIREHOSE: BLOCKS (header, commit, nodes, records)
            //
            /// Format from spec:
            /// 
            ///    [---  header  -------- ]   [----------------- data ---------------------------------]
            ///    [varint | header block ]   [varint | cid | data block]....[varint | cid | data block] 
            /// 
            MemoryStream blockStream = new MemoryStream();

            // header
            var firehoseFinal_RepoHeader = _db.GetRepoHeader();
            firehoseFinal_RepoHeader.WriteToStream(blockStream);

            // mst nodes
            // order descending by key depth so that root is written first
            foreach(var mstNode in nodesToSend.OrderByDescending(n => n.KeyDepth))
            {
                var (cid, dagCbor) = mstNodeCache[mstNode];
                DagCborObject.WriteToRepoStream(blockStream, cid, dagCbor);
            }

            // records
            foreach(var write in writes)
            {
                if(_db.RecordExists(write.Collection, write.Rkey))
                {
                    var record = _db.GetRepoRecord(write.Collection, write.Rkey);
                    DagCborObject.WriteToRepoStream(blockStream, record.Cid!, record.DataBlock);
                }
            }

            // commit
            var firehoseFinal_RepoCommit = _db.GetRepoCommit();
            DagCborObject.WriteToRepoStream(blockStream, firehoseFinal_RepoCommit.Cid!, firehoseFinal_RepoCommit.ToDagCborObject());


            //
            // FIREHOSE: OBJECT 2
            //
            long sequenceNumber = _db.GetNewSequenceNumberForFirehose();
            string createdDate = FirehoseEvent.GetNewCreatedDate();
            var object2Json = new JsonObject()
            {
                ["ops"] = firehoseState_Ops,
                ["rev"] = firehoseFinal_RepoCommit.Rev,
                ["seq"] = sequenceNumber,
                ["repo"] = _userDid,
                ["time"] = createdDate,
                ["blobs"] = new JsonArray(),
                ["since"] = before_repoCommit.Rev,
                ["blocks"] = "", // placeholder - will be replaced with byte[] below
                ["commit"] = firehoseFinal_RepoCommit.Cid!.ToString(),
                ["rebase"] = false,
                ["tooBig"] = false,
                ["prevData"] = before_repoCommit.RootMstNodeCid!.ToString()
            };

            var object2DagCbor = DagCborObject.FromJsonString(object2Json.ToString());

            // Replace fields with proper types (JSON serialization loses CID link and byte[] types)
            var object2Dict = (Dictionary<string, DagCborObject>)object2DagCbor.Value;
            
            // "blocks" - byte string
            object2Dict["blocks"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_BYTE_STRING, AdditionalInfo = 0, OriginalByte = 0 },
                Value = blockStream.ToArray()
            };

            // "commit" - CID link (TAG 42)
            object2Dict["commit"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = firehoseFinal_RepoCommit.Cid!
            };

            // "prevData" - CID link (TAG 42)
            object2Dict["prevData"] = new DagCborObject
            {
                Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                Value = before_repoCommit.RootMstNodeCid!
            };

            // "ops[].cid" - CID links (TAG 42)
            var opsArray = (List<DagCborObject>)object2Dict["ops"].Value;
            for (int i = 0; i < opsArray.Count; i++)
            {
                var opDict = (Dictionary<string, DagCborObject>)opsArray[i].Value;
                if (opDict.ContainsKey("cid"))
                {
                    var cidObj = opDict["cid"];
                    // Check if it's already a CID (shouldn't happen, but be safe)
                    if (cidObj.Value is CidV1 existingCid)
                    {
                        opDict["cid"] = new DagCborObject
                        {
                            Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                            Value = existingCid
                        };
                    }
                    else if (cidObj.Value is string cidString && cidString != "null")
                    {
                        CidV1 cidValue = CidV1.FromBase32(cidString);
                        opDict["cid"] = new DagCborObject
                        {
                            Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                            Value = cidValue
                        };
                    }
                    //
                    // This block of code is very important, do not remove it.
                    // Previously, we had a bug where "null" string was being sent as the cid,
                    // and during deletes it would crash the subscribeRepos connection and 
                    // retry constantly. For a "delete" operation, the "cid" field should be a simple value "null".
                    //
                    else if (cidObj.Value is string cidStrNull && cidStrNull == "null")
                    {
                        // turn into simple value
                        opDict["cid"] = new DagCborObject
                        {
                            Type = new DagCborType { MajorType = DagCborType.TYPE_SIMPLE_VALUE, AdditionalInfo = 0x16, OriginalByte = 0 },
                            Value = "null"
                        };
                    }
                }
                if (opDict.ContainsKey("prev"))
                {
                    var cidObj = opDict["prev"];
                    // Check if it's already a CID (shouldn't happen, but be safe)
                    if (cidObj.Value is CidV1 existingCid)
                    {
                        opDict["prev"] = new DagCborObject
                        {
                            Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                            Value = existingCid
                        };
                    }
                    else if (cidObj.Value is string cidString && cidString != "null")
                    {
                        CidV1 cidValue = CidV1.FromBase32(cidString);
                        opDict["prev"] = new DagCborObject
                        {
                            Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 },
                            Value = cidValue
                        };
                    }
                }
            }


            //
            // FIREHOSE: database object
            //
            FirehoseEvent firehoseEvent = new FirehoseEvent()
            {
                SequenceNumber = sequenceNumber,
                CreatedDate = createdDate,
                Header_op = header_op,
                Header_t = header_t,
                Header_DagCborObject = object1DagCbor,
                Body_DagCborObject = object2DagCbor
            };

            _db.InsertFirehoseEvent(firehoseEvent);


            //
            // Return
            //
            return results;
        }
        finally
        {
            Pds.GLOBAL_PDS_LOCK.Release();
        }
    }

    private void FixBlobRefs(List<ApplyWritesOperation> writes)
    {
        // loop through all writes and replace items that are probably cids.
        // convert from string to cid in the dagcbor

        foreach (var write in writes)
        {
            if (write.Record != null)
            {
                FixBlobRefsInDagCbor(null, write.Record);
            }
        }
    }

    private void FixBlobRefsInDagCbor(string? key, DagCborObject obj)
    {
        if (obj.Type.MajorType == DagCborType.TYPE_MAP)
        {
            var dict = obj.Value as Dictionary<string, DagCborObject>;
            if (dict != null)
            {
                foreach (var k in dict.Keys.ToList())
                {
                    FixBlobRefsInDagCbor(k, dict[k]);
                }                    
            }
        }
        else if (obj.Type.MajorType == DagCborType.TYPE_ARRAY)
        {
            var list = obj.Value as List<DagCborObject>;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    FixBlobRefsInDagCbor(null, list[i]);
                }
            }
        }
        else if (string.Equals("ref", key) 
            && obj.Type.MajorType == DagCborType.TYPE_TEXT 
            && obj.Value is string strValue 
            && strValue != "null"
            // check length of cid
            && strValue.Length == 59
            )
        {
            try
            {
                _logger.LogInfo($"Converting string '{strValue}' to CidV1");
                CidV1 cidValue = CidV1.FromBase32(strValue);
                obj.Type = new DagCborType { MajorType = DagCborType.TYPE_TAG, AdditionalInfo = 42, OriginalByte = 0 };
                obj.Value = cidValue;                
            }
            catch
            {
                // that's ok
            }
        }
    }


    public class ApplyWritesOperation
    {
        public required string Type;
        public required string Collection;
        public required string Rkey;
        public DagCborObject? Record = null;
    }

    public class ApplyWritesResult
    {
        public required string Type;
        public string? Uri = null;
        public CidV1? Cid = null;
        public string? ValidationStatus = null;
    }



    public class ApplyWritesType
    {
        public const string Create = "com.atproto.repo.applyWrites#create";
        public const string Update = "com.atproto.repo.applyWrites#update";
        public const string Delete = "com.atproto.repo.applyWrites#delete";
        public const string CreateResult = "com.atproto.repo.applyWrites#createResult";
        public const string UpdateResult = "com.atproto.repo.applyWrites#updateResult";
        public const string DeleteResult = "com.atproto.repo.applyWrites#deleteResult";
    }


    #endregion




    #region GET

    /// <summary>
    /// Gets record by collection and rkey.
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="rkey"></param>
    /// <returns></returns>
    public RepoRecord GetRecord(string collection, string rkey)
    {
        return _db.GetRepoRecord(collection, rkey);
    }

    public bool RecordExists(string collection, string rkey)
    {
        return _db.RecordExists(collection, rkey);
    }

    #endregion




    

    #region STREAM

    /// <summary>
    /// Loads entire repo from our database and writes it to the stream.
    /// For example, this is called by getRepo.
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public async Task WriteToStreamAsync(System.IO.Stream stream)
    {
        await Pds.GLOBAL_PDS_LOCK.WaitAsync();
        try
        {
            //
            // Header
            //
            var header = _db.GetRepoHeader();
            var headerDagCbor = header.ToDagCborObject();
            var headerDagCborBytes = headerDagCbor.ToBytes();
            var headerLengthVarInt = VarInt.FromLong((long)headerDagCborBytes.Length);
            await VarInt.WriteVarIntAsync(stream, headerLengthVarInt);
            await stream.WriteAsync(headerDagCborBytes, 0, headerDagCborBytes.Length);


            //
            // Repo Commit
            //
            var repoCommit = _db.GetRepoCommit();
            if (repoCommit == null)
            {
                _logger.LogError("Cannot write MST to stream: repo commit is null.");
                return;
            }

            var repoCommitDagCbor = repoCommit.ToDagCborObject();
            if (repoCommitDagCbor == null)
            {
                _logger.LogError("Cannot write MST to stream: failed to convert repo commit to DagCborObject.");
                return;
            }
            var repoCommitCid = repoCommit.Cid;
            if (repoCommitCid == null)
            {
                _logger.LogError("Cannot write MST to stream: repo commit CID is null.");
                return;
            }

            await WriteBlockAsync(stream, repoCommitCid, repoCommitDagCbor);


            //
            // MST Nodes
            //
            Mst mst = Mst.AssembleTreeFromItems(_db.GetAllRepoRecordMstItems());
            List<MstNode> allNodes = mst.FindAllNodes();
            Dictionary<MstNode, (CidV1, DagCborObject)> mstNodeCache = new Dictionary<MstNode, (CidV1, DagCborObject)>();
            foreach(var node in allNodes)
            {
                RepoMst.ConvertMstNodeToDagCbor(mstNodeCache, node);
            }


            foreach (MstNode mstNode in allNodes)
            {
                var (mstNodeCid, mstNodeDagCbor) = mstNodeCache[mstNode];
                await WriteBlockAsync(stream, mstNodeCid, mstNodeDagCbor);
            }

            //
            // Repo records (atproto records - like posts, profiles, etc)
            //
            var repoRecords = _db.GetAllRepoRecords();
            foreach (var repoRecord in repoRecords)
            {
                if (repoRecord.DataBlock == null || repoRecord.Cid == null)
                {
                    _logger.LogError($"Cannot write MST to stream: failed to convert repo record {repoRecord.Cid?.Base32} to DagCborObject.");
                    return;
                }

                await WriteBlockAsync(stream, repoRecord.Cid, repoRecord.DataBlock);
            }

        }
        finally
        {
            Pds.GLOBAL_PDS_LOCK.Release();
        }
    }

    /// <summary>
    /// Writing one record. The format is [VarInt | CidV1 | DagCborObject] (see Repo.cs)
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="cid"></param>
    /// <param name="dagCbor"></param>
    /// <returns></returns>
    public static async Task WriteBlockAsync(System.IO.Stream stream, CidV1 cid, DagCborObject dagCbor)
    {
        var cidBytes = cid.AllBytes;
        var dagCborBytes = dagCbor.ToBytes();
        var blockLengthVarInt = VarInt.FromLong((long)(cidBytes.Length + dagCborBytes.Length));

        await VarInt.WriteVarIntAsync(stream, blockLengthVarInt);
        await CidV1.WriteCidAsync(stream, cid);
        await stream.WriteAsync(dagCborBytes, 0, dagCborBytes.Length);
    }

    #endregion

}