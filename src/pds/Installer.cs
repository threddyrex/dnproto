

using dnproto.auth;
using dnproto.fs;
using dnproto.log;
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
        var config = new dnproto.pds.Config();
        config.Version = "0.0.001";
        config.ListenHost = "localhost";
        config.ListenPort = 5001;
        config.PdsHostname = pdsHostname!;
        config.PdsDid = "did:web:" + pdsHostname!;
        config.AvailableUserDomain = availableUserDomain!;
        var adminPassword = PasswordHasher.CreateNewAdminPassword();
        config.AdminHashedPassword = PasswordHasher.HashPassword(adminPassword);
        config.JwtSecret = JwtSecret.GenerateJwtSecret();
        config.UserHandle = userHandle!;
        config.UserDid = userDid!;
        var userPassword = PasswordHasher.CreateNewAdminPassword();
        config.UserHashedPassword = PasswordHasher.HashPassword(userPassword!);
        config.UserEmail = userEmail!;
        
        // Generate user keypair for signing commits
        var userKeyPair = dnproto.auth.KeyPair.Generate(dnproto.auth.KeyTypes.P256);
        config.UserPublicKeyMultibase = userKeyPair.PublicKeyMultibase;
        config.UserPrivateKeyMultibase = userKeyPair.PrivateKeyMultibase;


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
        var repoCommit = new RepoCommit();
        repoCommit.Did = config.UserDid;
        repoCommit.Rev = RecordKey.GenerateTid();
        repoCommit.RootMstNodeCid = mstNode.Cid;
        repoCommit.Version = 3;
        repoCommit.SignAndRecomputeCid(mstNode.Cid!, commitSigningFunction);


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
        db.InsertMstNode(mstNode); // no entries
        db.InsertUpdateRepoCommit(repoCommit);
        db.InsertUpdateRepoHeader(repoHeader);

    }

    #endregion
}