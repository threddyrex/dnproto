

using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.fs;
using dnproto.log;
using dnproto.mst;
using dnproto.pds;
using dnproto.repo;
using Microsoft.AspNetCore.Routing.Internal;


namespace dnproto.pds;

/// <summary>
/// Installer class for PDS.
/// 
/// Available methods (run in order):
/// 
///     InstallDb (creates database schema)
///     InstallAdminConfig (admin password)
///     InstallServerConfig (scheme, host, port)
///     InstallFeatureConfig
///
///     InstallConfig (creates full config for PDS and user)
///     InstallRepo (creates fresh repo for user)
/// 
/// Run the methods in order.
/// </summary>
public class Installer
{

    #region DB


    /// <summary>
    /// Install the database.
    /// If deleteExistingDb is true, will delete any existing database before installing.
    /// This will run the CreateTables regardless. So it's re-runnable for schema updates.
    /// </summary>
    public static void InstallDb(LocalFileSystem lfs, IDnProtoLogger logger, bool deleteExistingDb)
    {
        //
        // Paths
        //
        string dbDir = Path.Combine(lfs.GetDataDir(), "pds");
        string dbFilePath = Path.Combine(lfs.GetDataDir(), "pds", "pds.db");


        //
        // Check that the pds/db folder exists.
        //
        if (!Directory.Exists(dbDir))
        {
            logger.LogError($"PDS database directory does not exist: {dbDir}");
            return;
        }

        //
        // Check if they want to delete existing.
        //
        bool dbExists = File.Exists(dbFilePath);
        if(dbExists && deleteExistingDb)
        {
            logger.LogInfo("Deleting existing PDS database file.");
            File.Delete(dbFilePath);
        }
        else if(dbExists && !deleteExistingDb)
        {
            logger.LogInfo("PDS database file already exists. Will NOT delete.");
        }


        //
        // run create table commands
        //
        logger.LogInfo("Creating PDS database tables (if not exist).");
        using (var connection = SqliteDb.GetConnectionCreate(lfs.GetPath_PdsDb()))
        {
            connection.Open();
            PdsDb.CreateTable_Blob(connection, logger);
            PdsDb.CreateTable_Preferences(connection, logger);
            PdsDb.CreateTable_RepoHeader(connection, logger);
            PdsDb.CreateTable_RepoCommit(connection, logger);
            PdsDb.CreateTable_RepoRecord(connection, logger);
            PdsDb.CreateTable_SequenceNumber(connection, logger);
            PdsDb.CreateTable_FirehoseEvent(connection, logger);
            PdsDb.CreateTable_LogLevel(connection, logger);
            PdsDb.CreateTable_OauthRequest(connection, logger);
            PdsDb.CreateTable_OauthSession(connection, logger);
            PdsDb.CreateTable_LegacySession(connection, logger);
            PdsDb.CreateTable_AdminSession(connection, logger);
            PdsDb.CreateTable_Passkey(connection, logger);
            PdsDb.CreateTable_PasskeyChallenge(connection, logger);
            PdsDb.CreateTable_Statistic(connection, logger);
            PdsDb.CreateTable_ConfigProperty(connection, logger);
            connection.Close();
        }
    }



    #endregion


    #region ADMINCFG


    public static void InstallAdminConfig(LocalFileSystem lfs, IDnProtoLogger logger)
    {
        var adminPassword = PasswordHasher.CreateNewAdminPassword();
        PdsDb db = PdsDb.ConnectPdsDb(lfs, logger);
        db.SetConfigProperty("AdminHashedPassword", PasswordHasher.HashPassword(adminPassword));
        logger.LogInfo("username: admin");
        logger.LogInfo($"password: {adminPassword}");
    }


    #endregion




    #region SERVERCFG

    public static void InstallServerConfig(LocalFileSystem lfs, IDnProtoLogger logger, string listenScheme, string listenHost, int listenPort)
    {
        PdsDb db = PdsDb.ConnectPdsDb(lfs, logger);
        db.SetConfigProperty("ServerListenScheme", listenScheme);
        db.SetConfigProperty("ServerListenHost", listenHost);
        db.SetConfigPropertyInt("ServerListenPort", listenPort);
    }

    #endregion


    #region FEATURECFG

    public static void InstallFeatureConfig(LocalFileSystem lfs, IDnProtoLogger logger)
    {
        PdsDb db = PdsDb.ConnectPdsDb(lfs, logger);
        db.SetConfigPropertyBool("FeatureEnabled_AdminDashboard", true);
        db.SetConfigPropertyBool("FeatureEnabled_Oauth", true);
        db.SetConfigPropertyBool("FeatureEnabled_RequestCrawl", true);
        db.SetConfigPropertyBool("FeatureEnabled_Passkeys", true);
        db.SetConfigPropertyInt("LogRetentionDays", 10);
        
        db.SetConfigProperty("PdsCrawlers", "bsky.network");
    }

    #endregion





    #region REPO


    /// <summary>
    /// Install an empty repo into the database.
    /// It has one MST node with no entries, 
    /// one repo commit pointing to that MST node, 
    /// and one repo header pointing to that commit.
    /// </summary>
    /// <param name="dataDir"></param>
    /// <param name="logger"></param>
    public static void InstallRepo(LocalFileSystem lfs, IDnProtoLogger logger, Func<byte[], byte[]>? commitSigningFunction)
    {
        if(commitSigningFunction is null)
        {
            logger.LogError("Commit signing function is required to install repo.");
            return;
        }


        //
        // Connect to db
        //
        var db = PdsDb.ConnectPdsDb(lfs, logger);
        if (db == null)
        {
            logger.LogError("Failed to connect to PDS database to get config.");
            return;
        }


        //
        // Delete everything
        //
        logger.LogInfo("Deleting existing repo data (if any).");
        db.DeleteRepoCommit();
        db.DeleteAllRepoRecords();
        db.DeleteRepoHeader();
        db.DeleteAllFirehoseEvents();
        db.DeletePreferences();


        //
        // Increment sequence number
        //
        db.GetNewSequenceNumberForFirehose();

        //
        // Create empty MST node
        //
        var mstNode = new MstNode() { KeyDepth = 0, Entries = new List<MstEntry>() };
        Dictionary<MstNode, (CidV1, DagCborObject)> mstNodeCache = new Dictionary<MstNode, (CidV1, DagCborObject)>();
        RepoMst.ConvertMstNodeToDagCbor(mstNodeCache, mstNode);
        CidV1 rootCid = mstNodeCache[mstNode].Item1;


        //
        // Create repo commit.
        // First create unsigned, then sign it.
        //
        var repoCommit = new RepoCommit()
        {
            Did = db.GetConfigProperty("UserDid"),
            Version = 3,
            RootMstNodeCid = rootCid,
            Rev = RecordKey.GenerateTid()
        };

        repoCommit.SignAndRecomputeCid(rootCid, commitSigningFunction);

        if(repoCommit.Cid is null)
        {
            throw new Exception("Failed to create repo commit CID.");
        }

        //
        // Create repo header
        //
        var repoHeader = new RepoHeader
        {
            RepoCommitCid = repoCommit.Cid,
            Version = 1
        };


        //
        // Create fresh preferences
        //
        var prefs = new JsonObject()
        {
            ["preferences"] = new JsonArray()
            {
                new JsonObject()
                {
                    ["$type"] = "app.bsky.actor.defs#savedFeedsPrefV2",
                    ["items"] = new JsonArray()
                    {
                        new JsonObject()
                        {
                            ["id"] = RecordKey.GenerateTid(),
                            ["type"] = "timeline",
                            ["value"] = "following",
                            ["pinned"] = true
                        }
                    }
                },
                new JsonObject()
                {
                    ["$type"] = "app.bsky.actor.defs#personalDetailsPref",
                    ["birthDate"] = "1991-06-03T00:00:00.000Z"
                }
            }
        };

        string prefsJsonString = prefs.ToJsonString();

        //
        // Insert everything into the database
        //
        logger.LogInfo("Inserting initial MST node, repo commit, and repo header into the database.");
        db.InsertUpdateRepoCommit(repoCommit);
        db.InsertUpdateRepoHeader(repoHeader);
        db.InsertPreferences(prefsJsonString);


        //
        // Add a Bluesky profile record.
        //
        logger.LogInfo("Creating initial Bluesky profile record in the repo.");
        var userRepo = UserRepo.ConnectUserRepo(lfs, logger, db, commitSigningFunction, db.GetConfigProperty("UserDid"));
        var profileJsonObject = new JsonObject()
        {
            ["displayName"] = db.GetConfigProperty("UserHandle"),
            ["description"] = "This is my Bluesky profile."
        };

        DagCborObject profileRecord = DagCborObject.FromJsonString(profileJsonObject.ToJsonString());

        userRepo.ApplyWrites(new List<UserRepo.ApplyWritesOperation>
        {
            new UserRepo.ApplyWritesOperation
            {
                Type = UserRepo.ApplyWritesType.Create,
                Collection = "app.bsky.actor.profile",
                Rkey = "self",
                Record = profileRecord
            }
        }, null, null);
    }

    #endregion
}