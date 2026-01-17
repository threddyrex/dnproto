using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;
using dnproto.fs;
using dnproto.pds;
using dnproto.mst;

namespace dnproto.cli.commands;

public class RestoreAccount : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"actor"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[] {});
    }

    /// <summary>
    /// Restore an account - loads backup from disk, uploads to db on disk.
    /// This is for migrating account to this pds.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments.
        //

        // required
        string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");

        if (string.IsNullOrEmpty(dataDir) || string.IsNullOrEmpty(actor))
        {
            Logger.LogError("Missing required argument.");
            return;
        }


        //
        // Get local file system and actor info
        //
        LocalFileSystem? lfs = this.LocalFileSystem;
        ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

        if (lfs is null || actorInfo is null)
        {
            Logger.LogError("Failed to initialize lfs or resolve actor info.");
            return;
        }


        //
        // Get backup dir
        //
        string? backupDir = lfs?.GetPath_AccountBackupDir(actorInfo);
        if (string.IsNullOrEmpty(backupDir))
        {
            Logger.LogError("Failed to get backup directory.");
            return;
        }

        if(Directory.Exists(backupDir) == false)
        {
            Logger.LogError("Backup directory does not exist.");
            return;
        }

        //
        // Check readme.txt
        //
        string readmePath = Path.Combine(backupDir, "README.txt");
        if(!File.Exists(readmePath))
        {
            Logger.LogError("Failed to read readme file.");
            return;
        }


        //
        // Make sure did and pds exist
        //
        string? did = actorInfo?.Did;
        string? pds = actorInfo?.Pds;

        if(string.IsNullOrEmpty(did) || string.IsNullOrEmpty(pds))
        {
            Logger.LogError("Failed to resolve actor to did and pds.");
            return;
        }

        Logger.LogInfo($"Resolved handle to did: {did}");
        Logger.LogInfo($"Resolved handle to pds: {pds}");


        //
        // Connect to local db
        //
        PdsDb db = PdsDb.ConnectPdsDb(lfs!, Logger);
        IBlobDb blobDb = BlobDb.Create(lfs!, Logger);


        //
        // Prefs
        //
        string prefsFile = Path.Combine(backupDir, "prefs.json");
        Logger.LogInfo("");
        Logger.LogInfo($"----- PREFS -----");
        Logger.LogInfo($"Reading prefs file: {prefsFile}");

        if(File.Exists(prefsFile) == false)
        {
            Logger.LogError("Prefs file does not exist, exiting.");
            return;
        }
        string prefsJsonStr = File.ReadAllText(prefsFile);
        JsonNode? prefsJson = JsonNode.Parse(prefsJsonStr);

        if(prefsJson == null)
        {
            Logger.LogError("Failed to parse prefs json, exiting.");
            return;
        }

        if(db.GetPreferencesCount() == 0)
        {
            db.InsertPreferences(prefsJson.ToJsonString());
            Logger.LogInfo("Inserted preferences into database.");
        }
        else
        {
            db.UpdatePreferences(prefsJson.ToJsonString());
            Logger.LogInfo("Updated preferences in database.");
        }


        //
        // Get blobs
        //
        Logger.LogInfo("");
        Logger.LogInfo($"----- BLOBS -----");
        string blobsFile = Path.Combine(backupDir, "blobs.txt");
        string blobsDirectory = Path.Combine(backupDir, "blobs");
        Logger.LogInfo($"Reading blobs file: {blobsFile}");
        Logger.LogInfo($"Reading blobs directory: {blobsDirectory}");

        if(File.Exists(blobsFile) == false)
        {
            Logger.LogError("Blobs file does not exist, exiting.");
            return;
        }

        // delete all blobs
        Logger.LogInfo("Deleting all blobs from database.");
        db.DeleteAllBlobs();

        foreach(string blobCidStr in File.ReadAllLines(blobsFile))
        {
            Logger.LogInfo($"Restoring blob: {blobCidStr}");

            CidV1? blobCid = CidV1.FromBase32(blobCidStr);
            if(blobCid == null)
            {
                Logger.LogError($"Failed to parse blob cid: {blobCidStr}, exiting.");
                return;
            }

            string blobFileBytes = Path.Combine(blobsDirectory, blobCidStr);
            string blobFileMetadata = Path.Combine(blobsDirectory, $"{blobCidStr}.metadata.json");

            if(File.Exists(blobFileBytes) == false)
            {
                Logger.LogError($"Blob file does not exist: {blobFileBytes}, exiting.");
                return;
            }

            if(File.Exists(blobFileMetadata) == false)
            {
                Logger.LogError($"Blob metadata file does not exist: {blobFileMetadata}, exiting.");
                return;
            }

            byte[] blobData = File.ReadAllBytes(blobFileBytes);
            JsonObject? blobMetadata = JsonNode.Parse(File.ReadAllText(blobFileMetadata)) as JsonObject;
            if(blobMetadata == null)
            {
                Logger.LogError($"Failed to parse blob metadata json: {blobFileMetadata}, exiting.");
                return;
            }

            string contentType = blobMetadata["contentType"]?.GetValue<string>() ?? "";
            int contentLength = blobMetadata["contentLength"]?.GetValue<int>() ?? 0;
            Logger.LogInfo($" contentType: {contentType}, contentLength: {contentLength}");

            if(string.IsNullOrEmpty(contentType) || contentLength == 0)
            {
                Logger.LogError($"Invalid blob metadata: {blobFileMetadata}, exiting.");
                return;
            }

            db.InsertBlob(new Blob()
            {
                Cid = blobCidStr,
                ContentType = contentType,
                ContentLength = contentLength
            });
            blobDb.InsertBlobBytes(blobCidStr, blobData);
        }



        //
        // REPO (RepoHeader, RepoCommit, RepoRecord)
        //
        string repoFile = Path.Combine(backupDir, "repo.car");
        Logger.LogInfo("");
        Logger.LogInfo($"----- REPO -----");
        Logger.LogInfo($"Reading repo file: {repoFile}");


        // load MST
        List<MstItem> mstItems = RepoMst.LoadMstItemsFromRepo(repoFile, Logger);
        Dictionary<string, MstItem> mstItemsByRecordCid = new Dictionary<string, MstItem>();
        foreach(var mstItem in mstItems)
        {
            mstItemsByRecordCid[mstItem.Value] = mstItem;
        }

        // delete everything (keep prefs though - we just uploaded those)
        db.DeleteRepoCommit();
        db.DeleteRepoHeader();
        db.DeleteAllRepoRecords();
        db.DeleteAllFirehoseEvents();


        // walk repo
        Repo.WalkRepo(repoFile,
            (header) =>
            {
                Logger.LogInfo($"Inserting header. repoCommit: {header.RepoCommitCid}");
                db.InsertUpdateRepoHeader(header);
                return true;
            },
            (record) =>
            {
                if(record.IsAtProtoRecord())
                {
                    if(mstItemsByRecordCid.ContainsKey(record.Cid.Base32) == false)
                    {
                        Logger.LogError($"Couldn't find mstitem for record: {record.Cid.Base32}");
                        return false;
                    }

                    string fullKey = mstItemsByRecordCid[record.Cid.Base32].Key;
                    string collection = fullKey.Split("/")[0];
                    string rkey = fullKey.Split("/")[1];

                    if(string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(rkey))
                    {
                        Logger.LogError("Collection or rkey is null. Exiting.");
                        return false;
                    }

                    Logger.LogInfo($"ADDING. cid:{record.Cid}, atProtoType:{record.AtProtoType}");

                    db.InsertRepoRecord(collection, rkey, record.Cid, record.DataBlock);
                }
                else if(record.IsRepoCommit())
                {
                    RepoCommit? repoCommit = record.ToRepoCommit();
                    if(repoCommit is null)
                    {
                        Logger.LogError($"repoCommit is null.");
                        return false;
                    }

                    db.InsertUpdateRepoCommit(repoCommit);
                }
                else
                {
                    Logger.LogWarning($"skipping. cid:{record.Cid}, isMstNode:{RepoMst.IsMstNode(record)}");
                }

                return true;
            });        


        Logger.LogInfo("");


    }
}