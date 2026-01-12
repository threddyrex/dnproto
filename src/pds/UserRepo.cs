
using dnproto.fs;
using dnproto.log;
using dnproto.repo;
using Microsoft.Extensions.Primitives;
using System.Text.Json.Nodes;

namespace dnproto.pds;




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
    ///     1. MST (MstNode, MstEntry)
    ///     2. Repo Record (RepoRecord)
    ///     3. Repo Commit (RepoCommit)
    ///     4. Repo Header (RepoHeader)
    ///     5. Firehose (FirehoseEvent)
    /// 
    /// </summary>
    /// <param name="writes"></param>
    /// <returns></returns>
    public List<ApplyWritesResult> ApplyWrites(List<ApplyWritesOperation> writes)
    {
        Pds.GLOBAL_PDS_LOCK.Wait();
        try
        {
            var mst = MstDb.ConnectMstDb(_lfs, _logger, _db);
            List<ApplyWritesResult> results = new List<ApplyWritesResult>();


            //
            // FIREHOSE: for saving state
            //
            List<Guid> firehoseState_NodeObjectIds = new List<Guid>();
            List<(string collection, string rkey)> firehoseState_RecordKeyPaths = new List<(string collection, string rkey)>();
            JsonArray firehoseState_Ops = new JsonArray();

            RepoCommit firehoseBefore_OriginalRepoCommit = _db.GetRepoCommit();


            //
            // Loop through operations and do writes.
            //
            foreach(var write in writes)
            {
                _logger.LogInfo($"Applying write operation: {write.Type} on collection: {write.Collection} with rkey: {write.Rkey}");

                string uri = $"at://{_userDid}/{write.Collection}/{write.Rkey}";
                string fullKey = $"{write.Collection}/{write.Rkey}";


                switch(write.Type)
                {
                    //
                    // CREATE/UPDATE
                    //
                    case ApplyWritesType.Create:
                    case ApplyWritesType.Update:

                        if(write.Record is null)
                        {
                            _logger.LogError($"Update operation missing record for collection: {write.Collection} with rkey: {write.Rkey}");
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
                        // MST
                        //
                        (CidV1 originalRootMstNodeCid, 
                            CidV1 newRootMstNodeCid, 
                            List<Guid> updatedNodeObjectIds) = mst.PutEntry(fullKey, recordCid);


                        //
                        // REPO COMMIT
                        //
                        // (we don't send the commit for every iteration, but methods in
                        // MstDb require the commit to be updated for each write operation)
                        //
                        var repoCommit = _db.GetRepoCommit()!;
                        repoCommit.SignAndRecomputeCid(newRootMstNodeCid, _commitSigningFunction!);
                        _db.InsertUpdateRepoCommit(repoCommit);


                        //
                        // REPO HEADER
                        //
                        // (we don't send the commit for every iteration, but methods in
                        // MstDb require the commit to be updated for each write operation)
                        //
                        var repoHeader = _db.GetRepoHeader()!;
                        repoHeader.RepoCommitCid = repoCommit.Cid!;
                        _db.InsertUpdateRepoHeader(repoHeader);


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
                        // FIREHOSE: save state
                        //
                        foreach(var nodeObjectId in updatedNodeObjectIds)
                        {
                            if(!firehoseState_NodeObjectIds.Contains(nodeObjectId))
                            {
                                firehoseState_NodeObjectIds.Add(nodeObjectId);                                
                            }
                        }
                        firehoseState_RecordKeyPaths.Add((write.Collection, write.Rkey));
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

                        if(! _db.RecordExists(write.Collection, write.Rkey)) break;

                        //
                        // REPO RECORD
                        //
                        RepoRecord originalRepoRecord = _db.GetRepoRecord(write.Collection, write.Rkey);
                        CidV1 originalRepoRecordCid = originalRepoRecord.Cid;
                        _db.DeleteRepoRecord(write.Collection, write.Rkey);


                        //
                        // MST
                        //
                        (CidV1 originalRootMstNodeCid1, 
                            CidV1 newRootMstNodeCid1, 
                            List<Guid> updatedNodeObjectIds1) = mst.DeleteEntry($"{write.Collection}/{write.Rkey}");


                        //
                        // REPO COMMIT
                        //
                        // (we don't send the commit for every iteration, but methods in
                        // MstDb require the commit to be updated for each write operation)
                        //
                        var repoCommit_delete = _db.GetRepoCommit()!;
                        repoCommit_delete.SignAndRecomputeCid(newRootMstNodeCid1, _commitSigningFunction!);
                        _db.InsertUpdateRepoCommit(repoCommit_delete);


                        //
                        // REPO HEADER
                        //
                        // (we don't send the commit for every iteration, but methods in
                        // MstDb require the commit to be updated for each write operation)
                        //
                        var repoHeader_delete = _db.GetRepoHeader()!;
                        repoHeader_delete.RepoCommitCid = repoCommit_delete.Cid!;
                        _db.InsertUpdateRepoHeader(repoHeader_delete);

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
                        // FIREHOSE: save state
                        //
                        foreach(var nodeObjectId in updatedNodeObjectIds1)
                        {
                            if(!firehoseState_NodeObjectIds.Contains(nodeObjectId))
                            {
                                firehoseState_NodeObjectIds.Add(nodeObjectId);                                
                            }
                        }

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

            // commit
            var firehoseFinal_RepoCommit = _db.GetRepoCommit();
            DagCborObject.WriteToRepoStream(blockStream, firehoseFinal_RepoCommit.Cid!, firehoseFinal_RepoCommit.ToDagCborObject());

            // mst nodes
            foreach(var nodeObjectId in firehoseState_NodeObjectIds)
            {
                var mstNode = _db.GetMstNodeByObjectId(nodeObjectId);
                var mstEntries = _db.GetMstEntriesForNodeObjectId(nodeObjectId);
                DagCborObject mstNodeDagCbor = mstNode.ToDagCborObject(mstEntries);
                DagCborObject.WriteToRepoStream(blockStream, mstNode.Cid!, mstNodeDagCbor);
            }

            // records
            foreach(var (collection, rkey) in firehoseState_RecordKeyPaths)
            {
                if(_db.RecordExists(collection, rkey))
                {
                    var record = _db.GetRepoRecord(collection, rkey);
                    DagCborObject.WriteToRepoStream(blockStream, record.Cid!, record.DataBlock);
                }
            }


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
                ["since"] = firehoseBefore_OriginalRepoCommit.Rev,
                ["blocks"] = "", // placeholder - will be replaced with byte[] below
                ["commit"] = firehoseFinal_RepoCommit.Cid!.ToString(),
                ["rebase"] = false,
                ["tooBig"] = false,
                ["prevData"] = firehoseBefore_OriginalRepoCommit.Cid!.ToString()
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
                Value = firehoseBefore_OriginalRepoCommit.Cid!
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
            MstDb mst = MstDb.ConnectMstDb(_lfs, _logger, _db);
            var allMstNodes = _db.GetAllMstNodes();
            var allMstEntriesByNode = _db.GetAllMstEntriesByNodeObjectId();
            foreach (MstNode mstNode in allMstNodes)
            {
                if (mstNode.Cid == null)
                {
                    _logger.LogError("Cannot write MST to stream: MST node CID is null.");
                    return;
                }
                
                List<MstEntry> mstEntriesForNode = allMstEntriesByNode.ContainsKey((Guid) mstNode.NodeObjectId!) ? allMstEntriesByNode[(Guid) mstNode.NodeObjectId!] : new List<MstEntry>();
                
                var mstNodeDagCbor = mstNode.ToDagCborObject(mstEntriesForNode);
                if (mstNodeDagCbor == null)
                {
                    _logger.LogError($"Cannot write MST to stream: failed to convert MST node {mstNode.Cid?.Base32} to DagCborObject.");
                    return;
                }
                var mstNodeCid = mstNode.Cid;
                if (mstNodeCid == null)
                {
                    _logger.LogError("Cannot write MST to stream: MST node CID is null.");
                    return;
                }

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