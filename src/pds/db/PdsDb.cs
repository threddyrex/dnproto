using dnproto.log;
using dnproto.pds.db;
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


    #region INITIALIZE

    /// <summary>
    /// Initializes the PDS database on disk. Checks that the folder exists (in local data dir, in the "pds/db" sub dir).
    /// If already exists, it will fail.
    /// </summary>
    public static PdsDb? InitializePdsDb(string dataDir, IDnProtoLogger logger, bool force = false)
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
        
        logger.LogInfo($"Initializing database at: {dbPath}");


        //
        // Run through the initialize statements.
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


    public bool InsertConfig(Config config)
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
VALUES (@@ListenScheme, @ListenHost, @ListenPort, @PdsDid, @PdsHostname, @AvailableUserDomain, @AdminHashedPassword, @JwtSecret, @UserHandle, @UserDid, @UserHashedPassword, @UserEmail, @UserPublicKeyMultibase, @UserPrivateKeyMultibase)
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

    public Config GetConfig()
    {
        var config = new Config();
        
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



    public void InsertBlob(Blob blob)
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

    public void UpdateBlob(Blob blob)
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

    public Blob? GetBlobByCid(string cid)
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
                    var blob = new Blob
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
                        RepoCommitCid = repoCommitCid,
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
            command.Parameters.AddWithValue("@RepoCommitCid", repoHeader.RepoCommitCid);
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
            command.Parameters.AddWithValue("@RepoCommitCid", repoHeader.RepoCommitCid);
            command.Parameters.AddWithValue("@Version", repoHeader.Version);

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
Signature TEXT NOT NULL
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
                        Version = reader.GetInt32(reader.GetOrdinal("Version")),
                        Cid = reader.GetString(reader.GetOrdinal("Cid")),
                        RootMstNodeCid = reader.GetString(reader.GetOrdinal("RootMstNodeCid")),
                        Rev = reader.GetString(reader.GetOrdinal("Rev")),
                        PrevMstNodeCid = reader.IsDBNull(reader.GetOrdinal("PrevMstNodeCid")) ? null : reader.GetString(reader.GetOrdinal("PrevMstNodeCid")),
                        Signature = reader.GetString(reader.GetOrdinal("Signature"))
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
            command.Parameters.AddWithValue("@Cid", repoCommit.Cid);
            command.Parameters.AddWithValue("@RootMstNodeCid", repoCommit.RootMstNodeCid);
            command.Parameters.AddWithValue("@Rev", repoCommit.Rev);
            if(repoCommit.PrevMstNodeCid != null)
            {
                command.Parameters.AddWithValue("@PrevMstNodeCid", repoCommit.PrevMstNodeCid);
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
            command.Parameters.AddWithValue("@Cid", repoCommit.Cid);
            command.Parameters.AddWithValue("@RootMstNodeCid", repoCommit.RootMstNodeCid);
            command.Parameters.AddWithValue("@Rev", repoCommit.Rev);
            if(repoCommit.PrevMstNodeCid != null)
            {
                command.Parameters.AddWithValue("@PrevMstNodeCid", repoCommit.PrevMstNodeCid);
            }
            else
            {
                command.Parameters.AddWithValue("@PrevMstNodeCid", DBNull.Value);
            }
            command.Parameters.AddWithValue("@Signature", repoCommit.Signature);

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
Cid TEXT PRIMARY KEY,
LeftMstNodeCid TEXT
)
        ";
        
        command.ExecuteNonQuery();        
    }

    public MstNode? GetMstNode(string cid)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM MstNode WHERE Cid = @Cid";
            command.Parameters.AddWithValue("@Cid", cid);
            
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    var mstNode = new MstNode
                    {
                        Cid = reader.GetString(reader.GetOrdinal("Cid")),
                        LeftMstNodeCid = reader.IsDBNull(reader.GetOrdinal("LeftMstNodeCid")) ? null : reader.GetString(reader.GetOrdinal("LeftMstNodeCid"))
                    };

                    return mstNode;
                }
            }
        }
        
        return null;
    }

    public void InsertMstNode(MstNode mstNode)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO MstNode (Cid, LeftMstNodeCid)
VALUES (@Cid, @LeftMstNodeCid)
            ";
            command.Parameters.AddWithValue("@Cid", mstNode.Cid);
            if(mstNode.LeftMstNodeCid != null)
            {
                command.Parameters.AddWithValue("@LeftMstNodeCid", mstNode.LeftMstNodeCid);
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
        DeleteMstNode(mstNode.Cid);
    }

    public void DeleteMstNode(string cid)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM MstNode WHERE Cid = @Cid
            ";
            command.Parameters.AddWithValue("@Cid", cid);
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
MstNodeCid TEXT NOT NULL,
KeySuffix TEXT NOT NULL,
PrefixLength INTEGER NOT NULL,
TreeMstNodeCid TEXT,
RecordCid TEXT NOT NULL,
PRIMARY KEY (MstNodeCid, KeySuffix)
)
        ";
        
        command.ExecuteNonQuery();        
    }


    public List<MstEntry> GetMstEntriesForNode(string mstNodeCid)
    {
        var entries = new List<MstEntry>();

        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM MstEntry WHERE MstNodeCid = @MstNodeCid ORDER BY PrefixLength ASC";
            command.Parameters.AddWithValue("@MstNodeCid", mstNodeCid);
            
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    var entry = new MstEntry
                    {
                        MstNodeCid = reader.GetString(reader.GetOrdinal("MstNodeCid")),
                        KeySuffix = reader.GetString(reader.GetOrdinal("KeySuffix")),
                        PrefixLength = reader.GetInt32(reader.GetOrdinal("PrefixLength")),
                        TreeMstNodeCid = reader.IsDBNull(reader.GetOrdinal("TreeMstNodeCid")) ? null : reader.GetString(reader.GetOrdinal("TreeMstNodeCid")),
                        RecordCid = reader.GetString(reader.GetOrdinal("RecordCid"))
                    };

                    entries.Add(entry);
                }
            }
        }

        return entries;
    }

    public void InsertMstEntry(MstEntry mstEntry)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO MstEntry (MstNodeCid, KeySuffix, PrefixLength, TreeMstNodeCid, RecordCid)
VALUES (@MstNodeCid, @KeySuffix, @PrefixLength, @TreeMstNodeCid, @RecordCid)
            ";
            command.Parameters.AddWithValue("@MstNodeCid", mstEntry.MstNodeCid);
            command.Parameters.AddWithValue("@KeySuffix", mstEntry.KeySuffix);
            command.Parameters.AddWithValue("@PrefixLength", mstEntry.PrefixLength);
            if(mstEntry.TreeMstNodeCid != null)
            {
                command.Parameters.AddWithValue("@TreeMstNodeCid", mstEntry.TreeMstNodeCid);
            }
            else
            {
                command.Parameters.AddWithValue("@TreeMstNodeCid", DBNull.Value);
            }
            command.Parameters.AddWithValue("@RecordCid", mstEntry.RecordCid);

            command.ExecuteNonQuery();
        }
    }

    public void DeleteMstEntriesForNode(string mstNodeCid)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM MstEntry WHERE MstNodeCid = @MstNodeCid
            ";
            command.Parameters.AddWithValue("@MstNodeCid", mstNodeCid);
            command.ExecuteNonQuery();
        }
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
JsonData TEXT NOT NULL
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
INSERT INTO RepoRecord (Cid, JsonData)
VALUES (@Cid, @JsonData)
            ";
            command.Parameters.AddWithValue("@Cid", repoRecord.Cid);
            command.Parameters.AddWithValue("@JsonData", repoRecord.JsonData);

            command.ExecuteNonQuery();
        }
    }

    public RepoRecord? GetRepoRecord(string cid)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM RepoRecord WHERE Cid = @Cid LIMIT 1";
            command.Parameters.AddWithValue("@Cid", cid);
            
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    var repoRecord = new RepoRecord
                    {
                        Cid = reader.GetString(reader.GetOrdinal("Cid")),
                        JsonData = reader.GetString(reader.GetOrdinal("JsonData"))
                    };
                    return repoRecord;
                }
            }
        }

        return null;
    }

    public void DeleteRepoRecord(string cid)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM RepoRecord WHERE Cid = @Cid
            ";
            command.Parameters.AddWithValue("@Cid", cid);
            command.ExecuteNonQuery();
        }
    }

    #endregion
}