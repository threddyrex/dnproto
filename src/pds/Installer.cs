

using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.fs;
using dnproto.log;
using dnproto.pds;
using dnproto.repo;


namespace dnproto.pds;

/// <summary>
/// Installer class for PDS.
/// 
/// Available methods (run in this order):
/// 
///     1. InstallDb (can be called multiple times, to create/update schema)
///     2. InstallConfig (one-time only, creates full config in db)
///     3. InstallRepo (can be called multiple times, to reset repo)
/// 
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
        using (var connection = PdsDb.GetConnectionCreate(lfs))
        {
            connection.Open();
            PdsDb.CreateTable_Config(connection, logger);
            PdsDb.CreateTable_Blob(connection, logger);
            PdsDb.CreateTable_Preferences(connection, logger);
            PdsDb.CreateTable_RepoHeader(connection, logger);
            PdsDb.CreateTable_RepoCommit(connection, logger);
            PdsDb.CreateTable_MstNode(connection, logger);
            PdsDb.CreateTable_MstEntry(connection, logger);
            PdsDb.CreateTable_RepoRecord(connection, logger);
        }

    }



    #endregion




    #region CONFIG

    /// <summary>
    /// Install the config into the database.
    /// This is not re-runnable.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="dataDir"></param>
    /// <param name="pdsHostname"></param>
    /// <param name="availableUserDomain"></param>
    /// <param name="userHandle"></param>
    /// <param name="userDid"></param>
    /// <param name="userEmail"></param>
    public static void InstallConfig(LocalFileSystem lfs, IDnProtoLogger logger, string? pdsHostname, string? availableUserDomain, string? userHandle, string? userDid, string? userEmail)
    {
        //
        // Create fresh config
        //
        var userKeyPair = dnproto.auth.KeyPair.Generate(dnproto.auth.KeyTypes.P256);
        var adminPassword = PasswordHasher.CreateNewAdminPassword();
        var userPassword = PasswordHasher.CreateNewAdminPassword();
        var config = new Config()
        {
            ListenHost = "localhost",
            ListenPort = 5001,
            ListenScheme = "http",
            PdsHostname = pdsHostname!,
            PdsDid = "did:web:" + pdsHostname!,
            AvailableUserDomain = availableUserDomain!,
            AdminHashedPassword = PasswordHasher.HashPassword(adminPassword),
            JwtSecret = JwtSecret.GenerateJwtSecret(),
            UserHandle = userHandle!,
            UserDid = userDid!,
            UserHashedPassword = PasswordHasher.HashPassword(userPassword),
            UserEmail = userEmail!,
            UserPublicKeyMultibase = userKeyPair.PublicKeyMultibase,
            UserPrivateKeyMultibase = userKeyPair.PrivateKeyMultibase,
        };


        //
        // Insert config into db
        //
        PdsDb db = PdsDb.ConnectPdsDb(lfs, logger);

        bool insertResult = db.InsertConfig(config);
        if (insertResult == false)
        {
            throw new Exception("Failed to insert config into database.");
        }


        //
        // Print out stuff that the user will need.
        //
        logger.LogInfo("PDS installed successfully.");
        logger.LogInfo("");
        logger.LogInfo("Important stuff to remember:");
        logger.LogInfo("");
        logger.LogInfo($"   Admin password: {adminPassword}");
        logger.LogInfo($"   User password: {userPassword}");
        logger.LogInfo("");
        logger.LogInfo("    User signing keypair (for DID document and commit signing):");
        logger.LogInfo($"       Public key (multibase):  {userKeyPair.PublicKeyMultibase}");
        logger.LogInfo($"       Private key (multibase): {userKeyPair.PrivateKeyMultibase}");
        logger.LogInfo($"       DID Key:                 {userKeyPair.DidKey}");
        logger.LogInfo("");
        logger.LogInfo($"Copy this powershell:\n\n$adminPassword = '{adminPassword}';\n$userHandle = '{userHandle}';\n$userPassword = '{userPassword}';\n\n to set the admin and user passwords in your environment for use with powershell.\n");

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
        // Get config
        //
        Config config = db.GetConfig();
        

        //
        // Delete everything
        //
        logger.LogInfo("Deleting existing repo data (if any).");
        db.DeleteRepoCommit();
        db.DeleteAllMstNodes();
        db.DeleteAllMstEntries();
        db.DeleteAllRepoRecords();
        db.DeleteRepoHeader();

        //
        // Create Mst Node
        //
        var mstNode = new MstNode
        {
            NodeObjectId = Guid.NewGuid(),
            Cid = null, // to be set
            LeftMstNodeCid = null
        };

        mstNode.RecomputeCid(new List<MstEntry>());


        //
        // Create repo commit.
        // First create unsigned, then sign it.
        //
        var repoCommit = new RepoCommit()
        {
            Did = config.UserDid,
            Version = 3,
            RootMstNodeCid = mstNode.Cid!,
            Rev = RecordKey.GenerateTid()
        };

        repoCommit.SignAndRecomputeCid(mstNode.Cid!, commitSigningFunction);


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
        // Insert everything into the database
        //
        logger.LogInfo("Inserting initial MST node, repo commit, and repo header into the database.");
        db.InsertMstNode(mstNode); // no entries
        db.InsertUpdateRepoCommit(repoCommit);
        db.InsertUpdateRepoHeader(repoHeader);


        //
        // Add a Bluesky profile record.
        //
        logger.LogInfo("Creating initial Bluesky profile record in the repo.");
        var userRepo = UserRepo.ConnectUserRepo(lfs, logger, db, commitSigningFunction, config.UserDid);
        var profileJsonObject = new JsonObject()
        {
            ["displayName"] = config.UserHandle,
            ["description"] = "This is my Bluesky profile."
        };

        DagCborObject profileRecord = DagCborObject.FromJsonString(profileJsonObject.ToJsonString());
        userRepo.CreateRecord("app.bsky.actor.profile", profileRecord, rkey: "self");
    }

    #endregion
}