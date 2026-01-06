using dnproto.log;
using dnproto.repo;
using Microsoft.Data.Sqlite;

namespace dnproto.pds.db;

public class PdsDb
{
    public required string _dataDir;
    public required IDnProtoLogger _logger;


    public static bool DbFileExists(string dataDir)
    {
        string dbPath = Path.Combine(dataDir, "pds", "pds.db");
        return File.Exists(dbPath);
    }


    #region INSTALL

    /// <summary>
    /// Installs the PDS database on disk. Checks that the folder exists (in local data dir, in the "pds/db" sub dir).
    /// If already exists, it will fail.
    /// </summary>
    public static PdsDb? InstallPdsDb(string dataDir, IDnProtoLogger logger, bool force = false)
    {
        //
        // Paths
        //
        string dbDir = Path.Combine(dataDir, "pds");
        string dbPath = Path.Combine(dbDir, "pds.db");

        //
        // Check that the pds/db folder exists.
        //
        if (!Directory.Exists(dbDir))
        {
            logger.LogError($"PDS database directory does not exist: {dbDir}");
            return null;
        }

        //
        // Check that the database file does not already exist.
        //
        if (DbFileExists(dataDir) && !force)
        {
            logger.LogError($"PDS database file already exists: {dbPath}");
            return null;
        }


        //
        // Create connection string for the SQLite database.
        // It will create the db if it doesn't exist.
        //
        string connectionString = new SqliteConnectionStringBuilder {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
        
        logger.LogInfo($"Installing database at: {dbPath}");


        //
        // Run through the install statements.
        // If the db already exists, these will be a no-op.
        //
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            CreateTable_Config(connection, logger);
            CreateTable_Blob(connection, logger);
            CreateTable_Preferences(connection, logger);
            CreateTable_RepoHeader(connection, logger);
            CreateTable_RepoCommit(connection, logger);
            CreateTable_MstNode(connection, logger);
            CreateTable_MstEntry(connection, logger);
            CreateTable_RepoRecord(connection, logger);
        }
        
        logger.LogInfo("Database initialization complete.");


        //
        // Return PdsDb instance.
        //
        return new PdsDb
        {
            _dataDir = dataDir,
            _logger = logger
        };
    }
    #endregion


    #region CONNECT

    public static PdsDb? ConnectPdsDb(string dataDir, IDnProtoLogger logger)
    {
        //
        // Check that the pds/db folder exists.
        //
        string dbDir = Path.Combine(dataDir, "pds");

        if (!Directory.Exists(dbDir))
        {
            logger.LogError($"PDS database directory does not exist: {dbDir}");
            return null;
        }

        //
        // Return PdsDb instance.
        //
        return new PdsDb
        {
            _dataDir = dataDir,
            _logger = logger
        };
    }
    
    #endregion



    #region SQL

    public SqliteConnection GetConnection()
    {
        string dbPath = Path.Combine(_dataDir, "pds", "pds.db");
        string connectionString = new SqliteConnectionStringBuilder {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWrite
        }.ToString();

        var conn = new SqliteConnection(connectionString);
        conn.Open();

        return conn;
    }

    public SqliteConnection GetConnectionReadOnly()
    {
        string dbPath = Path.Combine(_dataDir, "pds", "pds.db");
        string connectionString = new SqliteConnectionStringBuilder {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        var conn = new SqliteConnection(connectionString);
        conn.Open();

        return conn;
    }


    #endregion

    #region CONFIG

    private static void CreateTable_Config(SqliteConnection connection, IDnProtoLogger logger)
    {
        //
        // Config table
        //
        logger.LogInfo("table: Config");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS Config (
    ListenScheme TEXT NOT NULL,
    ListenHost TEXT NOT NULL,
    ListenPort INTEGER NOT NULL,
    PdsDid TEXT NOT NULL,
    PdsHostname TEXT NOT NULL,
    AvailableUserDomain TEXT NOT NULL,
    AdminHashedPassword TEXT NOT NULL,
    JwtSecret TEXT NOT NULL,
    UserHandle TEXT NOT NULL,
    UserDid TEXT NOT NULL,
    UserHashedPassword TEXT NOT NULL,
    UserEmail TEXT NOT NULL,
    UserPublicKeyMultibase TEXT NOT NULL,
    UserPrivateKeyMultibase TEXT NOT NULL
)
        ";
            
            command.ExecuteNonQuery();        
    }


    public bool InsertConfig(DbConfig config)
    {
        if(GetConfigCount() > 0)
        {
            _logger.LogError("Config already exists in database.");
            return false;
        }

        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO Config (ListenScheme, ListenHost, ListenPort, PdsDid, PdsHostname, AvailableUserDomain, AdminHashedPassword, JwtSecret, UserHandle, UserDid, UserHashedPassword, UserEmail, UserPublicKeyMultibase, UserPrivateKeyMultibase)
VALUES (@ListenScheme, @ListenHost, @ListenPort, @PdsDid, @PdsHostname, @AvailableUserDomain, @AdminHashedPassword, @JwtSecret, @UserHandle, @UserDid, @UserHashedPassword, @UserEmail, @UserPublicKeyMultibase, @UserPrivateKeyMultibase)
            ";
            command.Parameters.AddWithValue("@ListenScheme", config.ListenScheme);
            command.Parameters.AddWithValue("@ListenHost", config.ListenHost);
            command.Parameters.AddWithValue("@ListenPort", config.ListenPort);
            command.Parameters.AddWithValue("@PdsDid", config.PdsDid);
            command.Parameters.AddWithValue("@PdsHostname", config.PdsHostname);
            command.Parameters.AddWithValue("@AvailableUserDomain", config.AvailableUserDomain);
            command.Parameters.AddWithValue("@AdminHashedPassword", config.AdminHashedPassword);
            command.Parameters.AddWithValue("@JwtSecret", config.JwtSecret);
            command.Parameters.AddWithValue("@UserHandle", config.UserHandle);
            command.Parameters.AddWithValue("@UserDid", config.UserDid);
            command.Parameters.AddWithValue("@UserHashedPassword", config.UserHashedPassword);
            command.Parameters.AddWithValue("@UserEmail", config.UserEmail);
            command.Parameters.AddWithValue("@UserPublicKeyMultibase", config.UserPublicKeyMultibase);
            command.Parameters.AddWithValue("@UserPrivateKeyMultibase", config.UserPrivateKeyMultibase);

            command.ExecuteNonQuery();
        }

        return true;
    }

    public DbConfig GetConfig()
    {
        var config = new DbConfig();
        
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM Config LIMIT 1";
            
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    config.ListenScheme = reader.GetString(reader.GetOrdinal("ListenScheme"));
                    config.ListenHost = reader.GetString(reader.GetOrdinal("ListenHost"));
                    config.ListenPort = reader.GetInt32(reader.GetOrdinal("ListenPort"));
                    config.PdsDid = reader.GetString(reader.GetOrdinal("PdsDid"));
                    config.PdsHostname = reader.GetString(reader.GetOrdinal("PdsHostname"));
                    config.AvailableUserDomain = reader.GetString(reader.GetOrdinal("AvailableUserDomain"));
                    config.AdminHashedPassword = reader.GetString(reader.GetOrdinal("AdminHashedPassword"));
                    config.JwtSecret = reader.GetString(reader.GetOrdinal("JwtSecret"));
                    config.UserHandle = reader.GetString(reader.GetOrdinal("UserHandle"));
                    config.UserDid = reader.GetString(reader.GetOrdinal("UserDid"));
                    config.UserHashedPassword = reader.GetString(reader.GetOrdinal("UserHashedPassword"));
                    config.UserEmail = reader.GetString(reader.GetOrdinal("UserEmail"));
                    config.UserPublicKeyMultibase = reader.GetString(reader.GetOrdinal("UserPublicKeyMultibase"));
                    config.UserPrivateKeyMultibase = reader.GetString(reader.GetOrdinal("UserPrivateKeyMultibase"));
                }
            }
        }
        
        return config;
    }

    public int GetConfigCount()
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Config";
            
            var result = command.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }
    }

    #endregion



    #region BLOB

    public static void CreateTable_Blob(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: Blob");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS Blob (
Cid TEXT PRIMARY KEY,
ContentType TEXT NOT NULL,
ContentLength INTEGER NOT NULL,
Bytes BLOB NOT NULL
)
        ";
        
        command.ExecuteNonQuery();        
    }


    public bool BlobExists(string cid)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Blob WHERE Cid = @Cid";
            command.Parameters.AddWithValue("@Cid", cid);
            
            var result = command.ExecuteScalar();
            int count = result != null ? Convert.ToInt32(result) : 0;
            return count > 0;
        }
    }



    public void InsertBlob(DbBlob blob)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO Blob (Cid, ContentType, ContentLength, Bytes)
VALUES (@Cid, @ContentType, @ContentLength, @Bytes)
            ";
            command.Parameters.AddWithValue("@Cid", blob.Cid);
            command.Parameters.AddWithValue("@ContentType", blob.ContentType);
            command.Parameters.AddWithValue("@ContentLength", blob.ContentLength);
            command.Parameters.AddWithValue("@Bytes", blob.Bytes);

            command.ExecuteNonQuery();
        }
    }

    public void UpdateBlob(DbBlob blob)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
UPDATE Blob
SET ContentType = @ContentType, ContentLength = @ContentLength, Bytes = @Bytes
WHERE Cid = @Cid
            ";
            command.Parameters.AddWithValue("@Cid", blob.Cid);
            command.Parameters.AddWithValue("@ContentType", blob.ContentType);
            command.Parameters.AddWithValue("@ContentLength", blob.ContentLength);
            command.Parameters.AddWithValue("@Bytes", blob.Bytes);

            command.ExecuteNonQuery();
        }
    }

    public DbBlob? GetBlobByCid(string cid)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM Blob WHERE Cid = @Cid LIMIT 1";
            command.Parameters.AddWithValue("@Cid", cid);
            
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    var blob = new DbBlob
                    {
                        Cid = reader.GetString(reader.GetOrdinal("Cid")),
                        ContentType = reader.GetString(reader.GetOrdinal("ContentType")),
                        ContentLength = reader.GetInt32(reader.GetOrdinal("ContentLength")),
                        Bytes = (byte[])reader["Bytes"]
                    };
                    return blob;
                }
            }
        }

        return null;
    }

    public List<string> ListBlobsWithCursor(string? cursor, int limit)
    {
        var blobs = new List<string>();
        
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            if(cursor == null)
            {
                command.CommandText = "SELECT Cid FROM Blob ORDER BY Cid ASC LIMIT @Limit";
            }
            else
            {
                command.CommandText = "SELECT Cid FROM Blob WHERE Cid > @Cursor ORDER BY Cid ASC LIMIT @Limit";
                command.Parameters.AddWithValue("@Cursor", cursor);
            }
            command.Parameters.AddWithValue("@Limit", limit);
            
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    blobs.Add(reader.GetString(reader.GetOrdinal("Cid")));
                }
            }
        }
        
        return blobs;
    }

    #endregion


    #region PREFS


    private static void CreateTable_Preferences(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: Preferences");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS Preferences (
Prefs TEXT NOT NULL
)
        ";
        
        command.ExecuteNonQuery();

    }


    public string GetPreferences()
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT Prefs FROM Preferences LIMIT 1";
            
            var result = command.ExecuteScalar();
            return result != null ? Convert.ToString(result) ?? "" : "";
        }
    }

    public int GetPreferencesCount()
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Preferences";
            
            var result = command.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }
    }

    public void InsertPreferences(string prefs)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO Preferences (Prefs)
VALUES (@Prefs)
            ";
            command.Parameters.AddWithValue("@Prefs", prefs);

            command.ExecuteNonQuery();
        }
    }

    public void UpdatePreferences(string prefs)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
UPDATE Preferences
SET Prefs = @Prefs
            ";
            command.Parameters.AddWithValue("@Prefs", prefs);

            command.ExecuteNonQuery();
        }
    }

    #endregion



    #region RPHDR

    public static void CreateTable_RepoHeader(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: RepoHeader");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS RepoHeader (
RepoCommitCid TEXT PRIMARY KEY,
Version INTEGER NOT NULL
)
        ";
        
        command.ExecuteNonQuery();        
    }

    public bool RepoHeaderExists()
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM RepoHeader";
            
            var result = command.ExecuteScalar();
            int count = result != null ? Convert.ToInt32(result) : 0;
            return count == 1;
        }
    }

    public RepoHeader? GetRepoHeader()
    {        
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM RepoHeader LIMIT 1";
            
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    var repoCommitCid = reader.GetString(reader.GetOrdinal("RepoCommitCid"));
                    var version = reader.GetInt32(reader.GetOrdinal("Version"));

                    return new RepoHeader
                    {
                        RepoCommitCid = CidV1.FromBase32(repoCommitCid),
                        Version = version
                    };
                }
            }
        }
        
        return null;
    }
    public void InsertUpdateRepoHeader(RepoHeader repoHeader)
    {
        if(RepoHeaderExists())
        {
            UpdateRepoHeader(repoHeader);
        }
        else
        {
            InsertRepoHeader(repoHeader);
        }
    }

    private void InsertRepoHeader(RepoHeader repoHeader)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO RepoHeader (RepoCommitCid, Version)
VALUES (@RepoCommitCid, @Version)
            ";
            command.Parameters.AddWithValue("@RepoCommitCid", repoHeader.RepoCommitCid?.GetBase32());
            command.Parameters.AddWithValue("@Version", repoHeader.Version);

            command.ExecuteNonQuery();
        }
    }

    private void UpdateRepoHeader(RepoHeader repoHeader)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
UPDATE RepoHeader
SET Version = @Version, RepoCommitCid = @RepoCommitCid
            ";
            command.Parameters.AddWithValue("@RepoCommitCid", repoHeader.RepoCommitCid?.GetBase32());
            command.Parameters.AddWithValue("@Version", repoHeader.Version);

            command.ExecuteNonQuery();
        }
    }

    public void DeleteRepoHeader()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM RepoHeader
            ";
            command.ExecuteNonQuery();
        }
    }



    #endregion




    #region RPCMMT

    public static void CreateTable_RepoCommit(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: RepoCommit");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS RepoCommit (
Version INTEGER NOT NULL,
Cid TEXT PRIMARY KEY,
RootMstNodeCid TEXT NOT NULL,
Rev TEXT NOT NULL,
PrevMstNodeCid TEXT,
Signature BLOB NOT NULL
)
        ";
        
        command.ExecuteNonQuery();        
    }

    public bool RepoCommitExists()
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM RepoCommit";
            
            var result = command.ExecuteScalar();
            int count = result != null ? Convert.ToInt32(result) : 0;
            return count == 0;
        }
    }

    public RepoCommit? GetRepoCommit()
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM RepoCommit LIMIT 1";
            
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    var repoCommit = new RepoCommit
                    {
                        Did = this.GetConfig().UserDid,
                        Version = reader.GetInt32(reader.GetOrdinal("Version")),
                        Cid = CidV1.FromBase32(reader.GetString(reader.GetOrdinal("Cid"))),
                        RootMstNodeCid = CidV1.FromBase32(reader.GetString(reader.GetOrdinal("RootMstNodeCid"))),
                        Rev = reader.GetString(reader.GetOrdinal("Rev")),
                        PrevMstNodeCid = reader.IsDBNull(reader.GetOrdinal("PrevMstNodeCid")) ? null : CidV1.FromBase32(reader.GetString(reader.GetOrdinal("PrevMstNodeCid"))),
                        Signature = reader.GetFieldValue<byte[]>(reader.GetOrdinal("Signature"))
                    };
                    return repoCommit;
                }
            }
        }
        
        return null;
    }

    public void InsertUpdateRepoCommit(RepoCommit repoCommit)
    {
        if(RepoCommitExists())
        {
            InsertRepoCommit(repoCommit);
        }
        else
        {
            UpdateRepoCommit(repoCommit);
        }
    }

    private void InsertRepoCommit(RepoCommit repoCommit)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO RepoCommit (Version, Cid, RootMstNodeCid, Rev, PrevMstNodeCid, Signature)
VALUES (@Version, @Cid, @RootMstNodeCid, @Rev, @PrevMstNodeCid, @Signature)
            ";
            command.Parameters.AddWithValue("@Version", repoCommit.Version);
            command.Parameters.AddWithValue("@Cid", repoCommit.Cid?.Base32);
            command.Parameters.AddWithValue("@RootMstNodeCid", repoCommit.RootMstNodeCid?.Base32);
            command.Parameters.AddWithValue("@Rev", repoCommit.Rev);
            if(repoCommit.PrevMstNodeCid != null)
            {
                command.Parameters.AddWithValue("@PrevMstNodeCid", repoCommit.PrevMstNodeCid.Base32);
            }
            else
            {
                command.Parameters.AddWithValue("@PrevMstNodeCid", DBNull.Value);
            }
            command.Parameters.AddWithValue("@Signature", repoCommit.Signature);

            command.ExecuteNonQuery();
        }
    }

    private void UpdateRepoCommit(RepoCommit repoCommit)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
UPDATE RepoCommit
SET Version = @Version, Cid = @Cid, RootMstNodeCid = @RootMstNodeCid, Rev = @Rev, PrevMstNodeCid = @PrevMstNodeCid, Signature = @Signature
            ";
            command.Parameters.AddWithValue("@Version", repoCommit.Version);
            command.Parameters.AddWithValue("@Cid", repoCommit.Cid?.Base32);
            command.Parameters.AddWithValue("@RootMstNodeCid", repoCommit.RootMstNodeCid?.Base32);
            command.Parameters.AddWithValue("@Rev", repoCommit.Rev);
            if(repoCommit.PrevMstNodeCid != null)
            {
                command.Parameters.AddWithValue("@PrevMstNodeCid", repoCommit.PrevMstNodeCid?.Base32);
            }
            else
            {
                command.Parameters.AddWithValue("@PrevMstNodeCid", DBNull.Value);
            }
            command.Parameters.AddWithValue("@Signature", repoCommit.Signature);

            command.ExecuteNonQuery();
        }
    }

    public void DeleteRepoCommit()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM RepoCommit
            ";
            command.ExecuteNonQuery();
        }
    }


    #endregion


    #region MSTNODE

    public static void CreateTable_MstNode(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: MstNode");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS MstNode (
NodeObjectId TEXT PRIMARY KEY,
Cid TEXT NOT NULL,
LeftMstNodeCid TEXT
)
        ";
        
        command.ExecuteNonQuery();        
    }

    public MstNode? GetMstNodeByCid(CidV1? cid)
    {
        if(cid == null)
        {
            throw new ArgumentException("cid cannot be null when retrieving MstNode by Cid.");
        }

        var node = new MstNode();

        using(var sqlConnection = GetConnectionReadOnly())
        {

            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM MstNode WHERE Cid = @Cid";
            command.Parameters.AddWithValue("@Cid", cid?.Base32);
            
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    node = CreateNodeObjectFromReader(reader);
                }
                else
                {
                    throw new ArgumentException($"No MstNode found with Cid: {cid?.Base32}");
                }
            }
        }

        return node.Cid == null ? null : node;
    }

    public MstNode? GetMstNodeByObjectId(Guid? objectId)
    {
        if(objectId == null)
        {
            return null;
        }

        MstNode? node = null;

        using(var sqlConnection = GetConnectionReadOnly())
        {

            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM MstNode WHERE NodeObjectId = @NodeObjectId";
            command.Parameters.AddWithValue("@NodeObjectId", objectId.ToString());
            
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    node = CreateNodeObjectFromReader(reader);
                }
            }
        }

        return node;
    }

    public bool MstNodeExistsByCid(CidV1? cid)
    {
        if(cid == null)
        {
            throw new ArgumentException("cid cannot be null when checking MstNode existence by Cid.");
        }

        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM MstNode WHERE Cid = @Cid";
            command.Parameters.AddWithValue("@Cid", cid?.Base32);
            
            var result = command.ExecuteScalar();
            int count = result != null ? Convert.ToInt32(result) : 0;
            return count > 0;
        }
    }



    public List<MstNode> GetAllMstNodes()
    {
        var nodeList = new List<MstNode>();

        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM MstNode";
            
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    var node = CreateNodeObjectFromReader(reader);
                    nodeList.Add(node);
                }
            }
        }

        return nodeList;
    }

    private MstNode CreateNodeObjectFromReader(SqliteDataReader reader)
    {
        var node = new MstNode
        {
            NodeObjectId = Guid.Parse(reader.GetString(reader.GetOrdinal("NodeObjectId"))),
            Cid = CidV1.FromBase32(reader.GetString(reader.GetOrdinal("Cid"))),
            LeftMstNodeCid = reader.IsDBNull(reader.GetOrdinal("LeftMstNodeCid")) ? null : CidV1.FromBase32(reader.GetString(reader.GetOrdinal("LeftMstNodeCid")))
        };

        return node;
    }



    public void InsertMstNode(MstNode mstNode)
    {
        if(mstNode.NodeObjectId == null)
        {
            throw new ArgumentException("MstNode.NodeObjectId cannot be null when inserting.");
        }

        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO MstNode (NodeObjectId, Cid, LeftMstNodeCid)
VALUES (@NodeObjectId, @Cid, @LeftMstNodeCid)
            ";
            command.Parameters.AddWithValue("@NodeObjectId", mstNode.NodeObjectId.ToString());
            command.Parameters.AddWithValue("@Cid", mstNode.Cid?.Base32);
            if(mstNode.LeftMstNodeCid != null)
            {
                command.Parameters.AddWithValue("@LeftMstNodeCid", mstNode.LeftMstNodeCid?.Base32);
            }
            else
            {
                command.Parameters.AddWithValue("@LeftMstNodeCid", DBNull.Value);
            }

            command.ExecuteNonQuery();
        }
    }


    public void DeleteMstNode(MstNode mstNode)
    {
        DeleteMstNodeByObjectId(mstNode.NodeObjectId);
    }

    public void DeleteMstNodeByObjectId(Guid? objectId)
    {
        if(objectId == null)
        {
            throw new ArgumentException("objectId cannot be null when deleting MstNode.");
        }

        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM MstNode WHERE NodeObjectId = @NodeObjectId
            ";
            command.Parameters.AddWithValue("@NodeObjectId", objectId.ToString());
            command.ExecuteNonQuery();
        }
    }

    public void DeleteAllMstNodes()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM MstNode
            ";
            command.ExecuteNonQuery();
        }
    }




    #endregion


    #region MSTENTRY

    public static void CreateTable_MstEntry(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: MstEntry");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS MstEntry (
NodeObjectId TEXT NOT NULL,
EntryIndex INTEGER NOT NULL,
KeySuffix TEXT NOT NULL,
PrefixLength INTEGER NOT NULL,
TreeMstNodeCid TEXT,
RecordCid TEXT NOT NULL,
PRIMARY KEY (NodeObjectId, KeySuffix)
)
        ";
        
        command.ExecuteNonQuery();        
    }


    private MstEntry CreateMstEntryObjectFromReader(SqliteDataReader reader)
    {
        var entry = new MstEntry
        {
            NodeObjectId = Guid.Parse(reader.GetString(reader.GetOrdinal("NodeObjectId"))),
            EntryIndex = reader.GetInt32(reader.GetOrdinal("EntryIndex")),
            KeySuffix = reader.GetString(reader.GetOrdinal("KeySuffix")),
            PrefixLength = reader.GetInt32(reader.GetOrdinal("PrefixLength")),
            TreeMstNodeCid = reader.IsDBNull(reader.GetOrdinal("TreeMstNodeCid")) ? null : CidV1.FromBase32(reader.GetString(reader.GetOrdinal("TreeMstNodeCid"))),
            RecordCid = reader.IsDBNull(reader.GetOrdinal("RecordCid")) ? null : CidV1.FromBase32(reader.GetString(reader.GetOrdinal("RecordCid")))
        };

        return entry;
    }


    public List<MstEntry> GetMstEntriesForNodeObjectId(Guid nodeObjectId)
    {
        var entries = new List<MstEntry>();

        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM MstEntry WHERE NodeObjectId = @NodeObjectId ORDER BY EntryIndex ASC";
            command.Parameters.AddWithValue("@NodeObjectId", nodeObjectId.ToString());
            
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    var entry = CreateMstEntryObjectFromReader(reader);
                    entries.Add(entry);
                }
            }
        }

        return entries;
    }


    public List<MstEntry> GetAllMstEntries()
    {
        var entries = new List<MstEntry>();

        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM MstEntry ORDER BY NodeObjectId ASC, EntryIndex ASC";
            
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    var entry = CreateMstEntryObjectFromReader(reader);
                    entries.Add(entry);
                }
            }
        }

        return entries;
    }


    public Dictionary<Guid, List<MstEntry>> GetAllMstEntriesByNodeObjectId()
    {
        List<MstEntry> allEntries = GetAllMstEntries();
        var dict = new Dictionary<Guid, List<MstEntry>>();

        foreach(MstEntry entry in allEntries)
        {
            if(entry.NodeObjectId is null)
            {
                continue;
            }

            if(!dict.ContainsKey((Guid)entry.NodeObjectId!))
            {
                dict[(Guid)entry.NodeObjectId!] = new List<MstEntry>();
            }

            dict[(Guid)entry.NodeObjectId!].Add(entry);
        }

        return dict;
    }



    public void InsertMstEntries(Guid nodeObjectId, List<MstEntry> entries)
    {
        foreach(MstEntry entry in entries)
        {
            InsertMstEntry(nodeObjectId, entry);
        }
    }

    public void InsertMstEntry(Guid nodeObjectId, MstEntry mstEntry)
    {
        
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO MstEntry (NodeObjectId, EntryIndex, KeySuffix, PrefixLength, TreeMstNodeCid, RecordCid)
VALUES (@NodeObjectId, @EntryIndex, @KeySuffix, @PrefixLength, @TreeMstNodeCid, @RecordCid)
            ";
            command.Parameters.AddWithValue("@NodeObjectId", nodeObjectId.ToString());
            command.Parameters.AddWithValue("@EntryIndex", mstEntry.EntryIndex);
            command.Parameters.AddWithValue("@KeySuffix", mstEntry.KeySuffix);
            command.Parameters.AddWithValue("@PrefixLength", mstEntry.PrefixLength);
            if(mstEntry.TreeMstNodeCid != null)
            {
                command.Parameters.AddWithValue("@TreeMstNodeCid", mstEntry.TreeMstNodeCid?.Base32);
            }
            else
            {
                command.Parameters.AddWithValue("@TreeMstNodeCid", DBNull.Value);
            }
            command.Parameters.AddWithValue("@RecordCid", mstEntry.RecordCid?.Base32 ?? (object)DBNull.Value);

            command.ExecuteNonQuery();
        }
    }

    public void DeleteMstEntriesForNode(Guid nodeObjectId)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM MstEntry WHERE NodeObjectId = @NodeObjectId
            ";
            command.Parameters.AddWithValue("@NodeObjectId", nodeObjectId.ToString());
            command.ExecuteNonQuery();
        }
    }


    public void DeleteAllMstEntries()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM MstEntry
            ";
            command.ExecuteNonQuery();
        }
    }


    public void ReplaceMstEntriesForNode(Guid nodeObjectId, List<MstEntry> entries)
    {
        if(nodeObjectId == Guid.Empty)
        {
            return;
        }

        DeleteMstEntriesForNode(nodeObjectId);
        InsertMstEntries(nodeObjectId, entries);
    }

    /// <summary>
    /// Replaces an existing MST node and its entries with a new node and entries.
    /// This happens when a sub-tree changed, thus changing the sub-tree's cid,
    /// which in turn changes this node's cid.
    /// </summary>
    /// <param name="oldCid"></param>
    /// <param name="newCid"></param>
    /// <param name="mstNode"></param>
    /// <param name="entries"></param>
    public void ReplaceMstNode(MstNode mstNode, List<MstEntry> entries)
    {
        if(mstNode.NodeObjectId == null)
        {
            throw new ArgumentException("MstNode.NodeObjectId cannot be null when replacing.");
        }

        DeleteMstNode(mstNode);
        DeleteMstEntriesForNode((Guid) mstNode.NodeObjectId);
        InsertMstNode(mstNode);
        InsertMstEntries((Guid) mstNode.NodeObjectId, entries);
    }

    #endregion


    #region REPORECORD

    public static void CreateTable_RepoRecord(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: RepoRecord");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS RepoRecord (
Cid TEXT PRIMARY KEY,
DagCborObject BLOB NOT NULL
)
        ";
        
        command.ExecuteNonQuery();        
    }

    public void InsertRepoRecord(RepoRecord repoRecord)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO RepoRecord (Cid, DagCborObject)
VALUES (@Cid, @DagCborObject)
            ";
            command.Parameters.AddWithValue("@Cid", repoRecord.Cid?.Base32);
            command.Parameters.AddWithValue("@DagCborObject", repoRecord.DataBlock.ToBytes());

            command.ExecuteNonQuery();
        }
    }

    public RepoRecord? GetRepoRecord(CidV1? cid)
    {
        if(cid == null)
        {
            return null;
        }
        
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM RepoRecord WHERE Cid = @Cid LIMIT 1";
            command.Parameters.AddWithValue("@Cid", cid?.Base32);
            
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    return RepoRecord.FromDagCborObject(CidV1.FromBase32(reader.GetString(reader.GetOrdinal("Cid"))),
                        DagCborObject.FromBytes(reader.GetFieldValue<byte[]>(reader.GetOrdinal("DagCborObject"))));
                }
            }
        }

        return null;
    }

    public List<RepoRecord> GetAllRepoRecords()
    {
        var repoRecords = new List<RepoRecord>();
        
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM RepoRecord";
            
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    var repoRecord = RepoRecord.FromDagCborObject(CidV1.FromBase32(reader.GetString(reader.GetOrdinal("Cid"))),
                        DagCborObject.FromBytes(reader.GetFieldValue<byte[]>(reader.GetOrdinal("DagCborObject"))));
                    repoRecords.Add(repoRecord);
                }
            }
        }
        
        return repoRecords;
    }

    public void DeleteRepoRecord(CidV1? cid)
    {
        if(cid == null)
        {
            return;
        }

        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM RepoRecord WHERE Cid = @Cid
            ";
            command.Parameters.AddWithValue("@Cid", cid?.Base32);
            command.ExecuteNonQuery();
        }
    }

    public void DeleteAllRepoRecords()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM RepoRecord
            ";
            command.ExecuteNonQuery();
        }
    }

    #endregion
}