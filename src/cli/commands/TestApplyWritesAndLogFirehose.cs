using System.Text.Json.Nodes;
using dnproto.pds;
using dnproto.repo;

namespace dnproto.cli.commands;

public class TestApplyWritesAndLogFirehose : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { });
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[] { "text" });
    }

    public override void DoCommand(Dictionary<string, string> arguments)
    {
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
        string? text = CommandLineInterface.GetArgumentValue(arguments, "text") ?? "Hello from TestApplyWritesAndLogFirehose";

        if (string.IsNullOrEmpty(dataDir))
        {
            Logger.LogError("dataDir argument is required.");
            return;
        }

        //
        // Initialize PDS (database, file system, repo)
        //
        PdsDb db;
        try
        {
            var lfs = fs.LocalFileSystem.Initialize(dataDir, Logger);
            db = PdsDb.ConnectPdsDb(lfs, Logger);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to connect to PDS database: {ex.Message}");
            return;
        }

        //
        // Get sequence number before ApplyWrites so we can find the new firehose event
        //
        long seqBefore = db.GetMostRecentlyUsedSequenceNumber();
        Logger.LogInfo($"Sequence number before ApplyWrites: {seqBefore}");

        //
        // Get repo commit before ApplyWrites
        //
        RepoCommit commitBefore;
        try
        {
            commitBefore = db.GetRepoCommit();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to get repo commit before ApplyWrites: {ex.Message}");
            return;
        }

        Logger.LogInfo("");
        Logger.LogInfo("=== REPO COMMIT BEFORE APPLYWRITES ===");
        PrintRepoCommit(commitBefore);

        //
        // Build ApplyWrites operation - create a post
        // Hardcoded rkey and createdAt for cross-implementation comparison
        //
        string rkey = "3testapplywriteskey";
        string collection = "app.bsky.feed.post";
        string createdAt = "2026-01-01T00:00:00.000Z";

        JsonNode postNode = new JsonObject
        {
            ["text"] = text,
            ["createdAt"] = createdAt
        };

        Logger.LogInfo("");
        Logger.LogInfo("=== APPLYWRITES INPUT ===");
        Logger.LogInfo($"Collection: {collection}");
        Logger.LogInfo($"Rkey:       {rkey}");
        Logger.LogInfo($"Text:       {text}");

        var operation = new UserRepo.ApplyWritesOperation
        {
            Type = UserRepo.ApplyWritesType.Create,
            Collection = collection,
            Rkey = rkey,
            Record = DagCborObject.FromJsonString(postNode.ToJsonString())
        };

        //
        // Serialize the record to DAG-CBOR and print hex + debug
        //
        Logger.LogInfo("");
        Logger.LogInfo("=== RECORD DAG-CBOR (before ApplyWrites) ===");
        byte[] recordBytes = operation.Record.ToBytes();
        Logger.LogInfo($"Record DAG-CBOR hex ({recordBytes.Length} bytes):");
        Logger.LogInfo(BitConverter.ToString(recordBytes).Replace("-", "").ToLowerInvariant());
        Logger.LogInfo($"Record DAG-CBOR debug:");
        Logger.LogInfo(DagCborObject.GetRecursiveDebugString(operation.Record, 0));

        //
        // Call ApplyWrites
        //
        var userRepo = UserRepo.ConnectUserRepo(
            fs.LocalFileSystem.Initialize(dataDir, Logger),
            Logger,
            db);

        List<UserRepo.ApplyWritesResult> results;
        try
        {
            results = userRepo.ApplyWrites(
                new List<UserRepo.ApplyWritesOperation> { operation },
                "127.0.0.1",
                "TestApplyWritesAndLogFirehose");
        }
        catch (Exception ex)
        {
            Logger.LogError($"ApplyWrites failed: {ex.Message}");
            return;
        }

        //
        // Print ApplyWrites results
        //
        Logger.LogInfo("");
        Logger.LogInfo("=== APPLYWRITES RESULTS ===");
        foreach (var result in results)
        {
            Logger.LogInfo($"Type:             {result.Type}");
            Logger.LogInfo($"Uri:              {result.Uri}");
            Logger.LogInfo($"Cid:              {result.Cid?.GetBase32() ?? "<null>"}");
            Logger.LogInfo($"ValidationStatus: {result.ValidationStatus}");
        }

        //
        // Get repo commit after ApplyWrites
        //
        RepoCommit commitAfter;
        try
        {
            commitAfter = db.GetRepoCommit();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to get repo commit after ApplyWrites: {ex.Message}");
            return;
        }

        Logger.LogInfo("");
        Logger.LogInfo("=== REPO COMMIT AFTER APPLYWRITES ===");
        PrintRepoCommit(commitAfter);

        //
        // Print the commit as DAG-CBOR
        //
        Logger.LogInfo("");
        Logger.LogInfo("=== COMMIT DAG-CBOR ===");
        DagCborObject commitDagCbor = commitAfter.ToDagCborObject();
        byte[] commitDagCborBytes = commitAfter.ToDagCborBytes();
        Logger.LogInfo($"Commit DAG-CBOR hex ({commitDagCborBytes.Length} bytes):");
        Logger.LogInfo(BitConverter.ToString(commitDagCborBytes).Replace("-", "").ToLowerInvariant());
        Logger.LogInfo($"Commit DAG-CBOR debug:");
        Logger.LogInfo(DagCborObject.GetRecursiveDebugString(commitDagCbor, 0));
        Logger.LogInfo($"Commit JSON:");
        Logger.LogInfo(JsonData.ConvertObjectToJsonString(commitDagCbor.GetRawValue()));

        //
        // Get the new firehose event
        //
        long seqAfter = db.GetMostRecentlyUsedSequenceNumber();
        Logger.LogInfo("");
        Logger.LogInfo($"Sequence number after ApplyWrites: {seqAfter}");

        for (long seq = seqBefore + 1; seq <= seqAfter; seq++)
        {
            FirehoseEvent? firehoseEvent;
            try
            {
                firehoseEvent = db.GetFirehoseEvent(seq);
            }
            catch
            {
                Logger.LogInfo($"No firehose event at sequence {seq}.");
                continue;
            }

            if (firehoseEvent == null)
            {
                Logger.LogInfo($"No firehose event at sequence {seq}.");
                continue;
            }

            Logger.LogInfo("");
            Logger.LogInfo($"=== FIREHOSE EVENT {seq} ===");
            Logger.LogInfo($"Created:          {firehoseEvent.CreatedDate}");
            Logger.LogInfo($"Header op:        {firehoseEvent.Header_op}");
            Logger.LogInfo($"Header t:         {firehoseEvent.Header_t}");

            //
            // Header DAG-CBOR
            //
            Logger.LogInfo("");
            Logger.LogInfo("=== FIREHOSE HEADER DAG-CBOR ===");
            byte[] headerBytes = firehoseEvent.Header_DagCborObject.ToBytes();
            Logger.LogInfo($"Header DAG-CBOR hex ({headerBytes.Length} bytes):");
            Logger.LogInfo(BitConverter.ToString(headerBytes).Replace("-", "").ToLowerInvariant());
            Logger.LogInfo($"Header JSON:");
            Logger.LogInfo(JsonData.ConvertObjectToJsonString(firehoseEvent.Header_DagCborObject.GetRawValue()));
            Logger.LogInfo($"Header DAG-CBOR debug:");
            Logger.LogInfo(DagCborObject.GetRecursiveDebugString(firehoseEvent.Header_DagCborObject, 0));

            //
            // Body DAG-CBOR
            //
            Logger.LogInfo("");
            Logger.LogInfo("=== FIREHOSE BODY DAG-CBOR ===");
            byte[] bodyBytes = firehoseEvent.Body_DagCborObject.ToBytes();
            Logger.LogInfo($"Body DAG-CBOR hex ({bodyBytes.Length} bytes):");
            Logger.LogInfo(BitConverter.ToString(bodyBytes).Replace("-", "").ToLowerInvariant());
            Logger.LogInfo($"Body JSON:");
            Logger.LogInfo(JsonData.ConvertObjectToJsonString(firehoseEvent.Body_DagCborObject.GetRawValue()));
            Logger.LogInfo($"Body DAG-CBOR debug:");
            Logger.LogInfo(DagCborObject.GetRecursiveDebugString(firehoseEvent.Body_DagCborObject, 0));

            //
            // Walk blocks inside the firehose body
            //
            var blocks = firehoseEvent.Body_DagCborObject.SelectObjectValue(new[] { "blocks" });
            if (blocks != null && blocks is byte[] blockBytes)
            {
                Logger.LogInfo("");
                Logger.LogInfo($"=== FIREHOSE BLOCKS ({blockBytes.Length} bytes) ===");

                using (var blockStream = new MemoryStream(blockBytes))
                {
                    Repo.WalkRepo(
                        blockStream,
                        (repoHeader) =>
                        {
                            Logger.LogInfo($"CAR HEADER:");
                            Logger.LogInfo($"   roots:   {repoHeader.RepoCommitCid?.GetBase32()}");
                            Logger.LogInfo($"   version: {repoHeader.Version}");
                            return true;
                        },
                        (repoRecord) =>
                        {
                            Logger.LogInfo("");
                            Logger.LogInfo($"BLOCK CID: {repoRecord.Cid.GetBase32()}");

                            byte[] blockDataBytes = repoRecord.DataBlock.ToBytes();
                            Logger.LogInfo($"BLOCK DAG-CBOR hex ({blockDataBytes.Length} bytes):");
                            Logger.LogInfo(BitConverter.ToString(blockDataBytes).Replace("-", "").ToLowerInvariant());

                            Logger.LogInfo($"BLOCK JSON:");
                            Logger.LogInfo(repoRecord.JsonString);

                            Logger.LogInfo($"BLOCK DAG-CBOR debug:");
                            Logger.LogInfo(DagCborObject.GetRecursiveDebugString(repoRecord.DataBlock, 0));
                            return true;
                        }
                    );
                }
            }
        }

        Logger.LogInfo("");
        Logger.LogInfo("=== DONE ===");
    }

    private void PrintRepoCommit(RepoCommit commit)
    {
        Logger.LogInfo($"Commit CID:        {commit.Cid?.GetBase32() ?? "<null>"}");
        Logger.LogInfo($"Root MST Node CID: {commit.RootMstNodeCid?.GetBase32() ?? "<null>"}");
        Logger.LogInfo($"Rev:               {commit.Rev}");
        Logger.LogInfo($"Version:           {commit.Version}");
        Logger.LogInfo($"Did:               {commit.Did}");
        Logger.LogInfo($"Prev MST Node CID: {commit.PrevMstNodeCid?.GetBase32() ?? "<null>"}");
        if (commit.Signature != null)
        {
            Logger.LogInfo($"Signature hex ({commit.Signature.Length} bytes):");
            Logger.LogInfo(BitConverter.ToString(commit.Signature).Replace("-", "").ToLowerInvariant());
        }
    }
}
