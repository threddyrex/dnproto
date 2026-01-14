using dnproto.fs;
using dnproto.log;
using dnproto.mst;
using dnproto.repo;
using Microsoft.Data.Sqlite;

namespace dnproto.pds;

public class PdsDb
{
    private LocalFileSystem _lfs;
    private IDnProtoLogger _logger;


    private PdsDb(LocalFileSystem lfs, IDnProtoLogger logger)
    {
        _lfs = lfs;
        _logger = logger;
    }   


    #region CONNECT

    public static PdsDb ConnectPdsDb(LocalFileSystem lfs, IDnProtoLogger logger)
    {
        //
        // Check that the pds/db folder exists.
        //
        string dbDir = Path.Combine(lfs.GetDataDir(), "pds");
        string dbFilePath = Path.Combine(lfs.GetDataDir(), "pds", "pds.db");

        if (!Directory.Exists(dbDir))
        {
            throw new Exception($"PDS database directory does not exist: {dbDir}");
        }

        if (!File.Exists(dbFilePath))
        {
            throw new Exception($"PDS database file does not exist: {dbFilePath}");
        }

        //
        // Return PdsDb instance.
        //
        return new PdsDb(lfs, logger);
    }
    
    #endregion



    #region SQL

    public SqliteConnection GetConnection()
    {
        return GetConnection(_lfs);
    }

    public static SqliteConnection GetConnection(LocalFileSystem lfs)
    {
        string dbPath = lfs.GetPath_PdsDb();
        string connectionString = new SqliteConnectionStringBuilder {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWrite
        }.ToString();

        var conn = new SqliteConnection(connectionString);
        conn.Open();

        return conn;
    }

    public static SqliteConnection GetConnectionCreate(LocalFileSystem lfs)
    {
        string dbPath = lfs.GetPath_PdsDb();
        string connectionString = new SqliteConnectionStringBuilder {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        var conn = new SqliteConnection(connectionString);
        conn.Open();

        return conn;
    }


    public SqliteConnection GetConnectionReadOnly()
    {
        string dbPath = _lfs.GetPath_PdsDb();
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

    public static void CreateTable_Config(SqliteConnection connection, IDnProtoLogger logger)
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
    UserPrivateKeyMultibase TEXT NOT NULL,
    UserIsActive INTEGER NOT NULL
)
        ";
            
            command.ExecuteNonQuery();        
    }


    public bool InsertConfig(Config config)
    {
        if(GetConfigCount() > 0)
        {
            throw new Exception("Config already exists in database.");
        }

        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO Config (ListenScheme, ListenHost, ListenPort, PdsDid, PdsHostname, AvailableUserDomain, AdminHashedPassword, JwtSecret, UserHandle, UserDid, UserHashedPassword, UserEmail, UserPublicKeyMultibase, UserPrivateKeyMultibase, UserIsActive)
VALUES (@ListenScheme, @ListenHost, @ListenPort, @PdsDid, @PdsHostname, @AvailableUserDomain, @AdminHashedPassword, @JwtSecret, @UserHandle, @UserDid, @UserHashedPassword, @UserEmail, @UserPublicKeyMultibase, @UserPrivateKeyMultibase, @UserIsActive)
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
            command.Parameters.AddWithValue("@UserIsActive", config.UserIsActive ? 1 : 0);
            command.ExecuteNonQuery();
        }

        return true;
    }

    public Config GetConfig()
    {        
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM Config LIMIT 1";
            
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    var config = new Config()
                    {
                        ListenScheme = reader.GetString(reader.GetOrdinal("ListenScheme")),
                        ListenHost = reader.GetString(reader.GetOrdinal("ListenHost")),
                        ListenPort = reader.GetInt32(reader.GetOrdinal("ListenPort")),
                        PdsDid = reader.GetString(reader.GetOrdinal("PdsDid")),
                        PdsHostname = reader.GetString(reader.GetOrdinal("PdsHostname")),
                        AvailableUserDomain = reader.GetString(reader.GetOrdinal("AvailableUserDomain")),
                        AdminHashedPassword = reader.GetString(reader.GetOrdinal("AdminHashedPassword")),
                        JwtSecret = reader.GetString(reader.GetOrdinal("JwtSecret")),
                        UserHandle = reader.GetString(reader.GetOrdinal("UserHandle")),
                        UserDid = reader.GetString(reader.GetOrdinal("UserDid")),
                        UserHashedPassword = reader.GetString(reader.GetOrdinal("UserHashedPassword")),
                        UserEmail = reader.GetString(reader.GetOrdinal("UserEmail")),
                        UserPublicKeyMultibase = reader.GetString(reader.GetOrdinal("UserPublicKeyMultibase")),
                        UserPrivateKeyMultibase = reader.GetString(reader.GetOrdinal("UserPrivateKeyMultibase")),
                        UserIsActive = reader.GetInt32(reader.GetOrdinal("UserIsActive")) != 0
                    };

                    return config;
                }
                else
                {
                    throw new Exception("No config found in database.");
                }
            }
        }
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

    public void DeleteConfig()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM Config
            ";
            command.ExecuteNonQuery();
        }
    }


    #endregion


    #region USERACTIVE

    public bool IsUserActive()
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT UserIsActive FROM Config LIMIT 1";
            
            var result = command.ExecuteScalar();
            if(result is null)
            {
                throw new Exception("No config found in database.");
            }

            return Convert.ToInt32(result) != 0;
        }
    }


    public void SetUserActive(bool isActive)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "UPDATE Config SET UserIsActive = @UserIsActive";
            command.Parameters.AddWithValue("@UserIsActive", isActive ? 1 : 0);
            command.ExecuteNonQuery();
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


    public static void CreateTable_Preferences(SqliteConnection connection, IDnProtoLogger logger)
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
    public void DeletePreferences()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "DELETE FROM Preferences";
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

    public RepoHeader GetRepoHeader()
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
        
        throw new Exception("No RepoHeader found in database.");
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

    public RepoCommit GetRepoCommit()
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

                    if(repoCommit.Cid is null)
                    {
                        throw new Exception("RepoCommit CID is null.");
                    }
                    
                    return repoCommit;
                }
                else
                {
                    throw new Exception("No RepoCommit found in database.");
                }
            }
        }
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
        ValidateRepoCommit(repoCommit);

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

    public void UpdateRepoCommit(RepoCommit repoCommit)
    {
        ValidateRepoCommit(repoCommit);

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

    private void ValidateRepoCommit(RepoCommit repoCommit)
    {
        if(repoCommit.Cid is null 
            || string.IsNullOrEmpty(repoCommit.Rev)
            || repoCommit.Signature is null
            || repoCommit.Signature.Length == 0
            )
        {
            throw new Exception("RepoCommit in the db needs Rev, Signature, Cid.");
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





    #region REPORECORD

    public static void CreateTable_RepoRecord(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: RepoRecord");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS RepoRecord (
Collection TEXT NOT NULL,
Rkey TEXT NOT NULL,
Cid TEXT NOT NULL,
DagCborObject BLOB NOT NULL,
PRIMARY KEY (Collection, Rkey)
);
        ";
        
        command.ExecuteNonQuery();        
    }

    public void InsertRepoRecord(string collection, string rkey, CidV1 cid, DagCborObject dagCborObject)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO RepoRecord (Collection, Rkey, Cid, DagCborObject)
VALUES (@Collection, @Rkey, @Cid, @DagCborObject)
            ";
            command.Parameters.AddWithValue("@Collection", collection);
            command.Parameters.AddWithValue("@Rkey", rkey);
            command.Parameters.AddWithValue("@Cid", cid.Base32);
            command.Parameters.AddWithValue("@DagCborObject", dagCborObject.ToBytes());
            command.ExecuteNonQuery();
        }
    }

    public RepoRecord GetRepoRecord(string collection, string rkey)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM RepoRecord WHERE Collection = @Collection AND Rkey = @Rkey LIMIT 1";
            command.Parameters.AddWithValue("@Collection", collection);
            command.Parameters.AddWithValue("@Rkey", rkey);
            
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    return RepoRecord.FromDagCborObject(CidV1.FromBase32(reader.GetString(reader.GetOrdinal("Cid"))),
                        DagCborObject.FromBytes(reader.GetFieldValue<byte[]>(reader.GetOrdinal("DagCborObject"))));
                }
            }
        }

        throw new Exception($"RepoRecord not found for collection: {collection}, rkey: {rkey}");

    }

    /// <summary>
    /// Gets the raw CID and DAG-CBOR bytes for a record, without re-serializing.
    /// This is needed when serving records over the wire, since the CID must match the exact bytes.
    /// </summary>
    public (CidV1 Cid, byte[] DagCborBytes) GetRepoRecordRawBytes(string collection, string rkey)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT Cid, DagCborObject FROM RepoRecord WHERE Collection = @Collection AND Rkey = @Rkey LIMIT 1";
            command.Parameters.AddWithValue("@Collection", collection);
            command.Parameters.AddWithValue("@Rkey", rkey);
            
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    var cid = CidV1.FromBase32(reader.GetString(reader.GetOrdinal("Cid")));
                    var dagCborBytes = reader.GetFieldValue<byte[]>(reader.GetOrdinal("DagCborObject"));
                    return (cid, dagCborBytes);
                }
            }
        }

        throw new Exception($"RepoRecord not found for collection: {collection}, rkey: {rkey}");
    }

    public bool RecordExists(string collection, string rkey)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT 1 FROM RepoRecord WHERE Collection = @Collection AND Rkey = @Rkey LIMIT 1";
            command.Parameters.AddWithValue("@Collection", collection);
            command.Parameters.AddWithValue("@Rkey", rkey);
            
            var result = command.ExecuteScalar();
            return result != null;
        }
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

    public void DeleteRepoRecord(string collection, string rkey)
    {
        if(string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(rkey))
        {
            return;
        }

        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM RepoRecord WHERE Collection = @Collection AND Rkey = @Rkey
            ";
            command.Parameters.AddWithValue("@Collection", collection);
            command.Parameters.AddWithValue("@Rkey", rkey);
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

    public List<String> GetUniqueCollections()
    {
        var collections = new List<string>();
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT DISTINCT Collection FROM RepoRecord";
            
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    collections.Add(reader.GetString(reader.GetOrdinal("Collection")));
                }
            }
        }
        return collections;
    }


    public List<(string rkey, RepoRecord)> ListRepoRecordsByCollection(string collection, int limit = 100, string? cursor = null)
    {
        if(string.IsNullOrEmpty(cursor))
        {
            cursor = "0";
        }

        var repoRecords = new List<(string rkey, RepoRecord)>();
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT * FROM RepoRecord
WHERE Collection = @Collection
AND Rkey > @Cursor
ORDER BY Rkey ASC
LIMIT @Limit
            ";
            command.Parameters.AddWithValue("@Collection", collection);
            command.Parameters.AddWithValue("@Cursor", cursor);
            command.Parameters.AddWithValue("@Limit", limit);

            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    var repoRecord = RepoRecord.FromDagCborObject(CidV1.FromBase32(reader.GetString(reader.GetOrdinal("Cid"))),
                        DagCborObject.FromBytes(reader.GetFieldValue<byte[]>(reader.GetOrdinal("DagCborObject"))));
                    repoRecords.Add((reader.GetString(reader.GetOrdinal("Rkey")), repoRecord));
                }
            }
        }
        return repoRecords;
    }

    #endregion


    #region SEQ

    private static object SequenceNumberLock = new object();

    public static void CreateTable_SequenceNumber(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: SequenceNumber");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS SequenceNumber (
Seq INTEGER NOT NULL
);
        ";
        
        command.ExecuteNonQuery();        
    }


    public long GetNewSequenceNumberForFirehose()
    {
        lock(SequenceNumberLock)
        {
            long currentSeq = InternalGetCurrentSequenceNumber();
            DeleteSequenceNumber();
            long newSeq = currentSeq + 1;
            InternalInsertSequenceNumber(newSeq);
            return newSeq;
        }
    }

    public long GetMostRecentlyUsedSequenceNumber()
    {
        lock(SequenceNumberLock)
        {
            return InternalGetCurrentSequenceNumber();
        }
    }


    private long InternalGetCurrentSequenceNumber()
    {
        lock(SequenceNumberLock)
        {
            using(var sqlConnection = GetConnectionReadOnly())
            {
                var command = sqlConnection.CreateCommand();
                command.CommandText = "SELECT Seq FROM SequenceNumber LIMIT 1";
                
                var result = command.ExecuteScalar();
                return result != null ? Convert.ToInt64(result) : 0;
            }
        }
    }    

    private void InternalInsertSequenceNumber(long seq)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO SequenceNumber (Seq) VALUES (@Seq);
            ";
            command.Parameters.AddWithValue("@Seq", seq);
            command.ExecuteNonQuery();
        }
    }




    public void DeleteSequenceNumber()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM SequenceNumber
            ";
            command.ExecuteNonQuery();
        }
    }



    #endregion


    #region FIREHOSE

    public static void CreateTable_FirehoseEvent(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: FirehoseEvent");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS FirehoseEvent (
SequenceNumber INTEGER PRIMARY KEY,
CreatedDate TEXT NOT NULL,
Header_op INTEGER NOT NULL,
Header_t TEXT,
Header_DagCborObject BLOB NOT NULL,
Body_DagCborObject BLOB NOT NULL
);
        ";
        
        command.ExecuteNonQuery();        
    }
    

    public void InsertFirehoseEvent(FirehoseEvent firehoseEvent)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO FirehoseEvent (SequenceNumber, CreatedDate, Header_op, Header_t, Header_DagCborObject, Body_DagCborObject)
VALUES (@SequenceNumber, @CreatedDate, @Header_op, @Header_t, @Header_DagCborObject, @Body_DagCborObject)
            ";
            command.Parameters.AddWithValue("@SequenceNumber", firehoseEvent.SequenceNumber);
            command.Parameters.AddWithValue("@CreatedDate", firehoseEvent.CreatedDate);
            command.Parameters.AddWithValue("@Header_op", firehoseEvent.Header_op);
            command.Parameters.AddWithValue("@Header_t", firehoseEvent.Header_t);
            command.Parameters.AddWithValue("@Header_DagCborObject", firehoseEvent.Header_DagCborObject.ToBytes());
            command.Parameters.AddWithValue("@Body_DagCborObject", firehoseEvent.Body_DagCborObject.ToBytes());

            command.ExecuteNonQuery();
        }
    }

    public FirehoseEvent GetFirehoseEvent(long sequenceNumber)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT SequenceNumber, CreatedDate, Header_op, Header_t, Header_DagCborObject, Body_DagCborObject
FROM FirehoseEvent
WHERE SequenceNumber = @SequenceNumber
LIMIT 1
            ";
            command.Parameters.AddWithValue("@SequenceNumber", sequenceNumber);

            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    var firehoseEvent = new FirehoseEvent
                    {
                        SequenceNumber = reader.GetInt32(reader.GetOrdinal("SequenceNumber")),
                        CreatedDate = reader.GetString(reader.GetOrdinal("CreatedDate")),
                        Header_op = reader.GetInt32(reader.GetOrdinal("Header_op")),
                        Header_t = reader.IsDBNull(reader.GetOrdinal("Header_t")) ? null : reader.GetString(reader.GetOrdinal("Header_t")),
                        Header_DagCborObject = DagCborObject.FromBytes((byte[])reader["Header_DagCborObject"]),
                        Body_DagCborObject = DagCborObject.FromBytes((byte[])reader["Body_DagCborObject"])
                    };
                    return firehoseEvent;
                }
            }
        }

        throw new Exception($"FirehoseEvent with SequenceNumber {sequenceNumber} not found.");
    }


    /// <summary>
    /// Gets firehose events after the specified cursor (sequence number).
    /// If cursor is null, returns events from the beginning.
    /// </summary>
    /// <param name="cursor">The sequence number to start after (exclusive)</param>
    /// <param name="limit">Maximum number of events to return</param>
    /// <returns>List of FirehoseEvents ordered by SequenceNumber ascending</returns>
    public List<FirehoseEvent> GetFirehoseEventsForSubscribeRepos(long cursor, int limit = 100, int numHoursLookBack = 12)
    {
        var events = new List<FirehoseEvent>();

        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();

            string afterDate = FirehoseEvent.GetCreatedDateMinusHours(numHoursLookBack);
            command.CommandText = @"
SELECT SequenceNumber, CreatedDate, Header_op, Header_t, Header_DagCborObject, Body_DagCborObject
FROM FirehoseEvent
WHERE SequenceNumber > @Cursor AND CreatedDate >= @AfterDate
ORDER BY SequenceNumber ASC
LIMIT @Limit
            ";
            command.Parameters.AddWithValue("@Cursor", cursor);
            command.Parameters.AddWithValue("@Limit", limit);
            command.Parameters.AddWithValue("@AfterDate", afterDate);

            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    var firehoseEvent = new FirehoseEvent
                    {
                        SequenceNumber = reader.GetInt64(reader.GetOrdinal("SequenceNumber")),
                        CreatedDate = reader.GetString(reader.GetOrdinal("CreatedDate")),
                        Header_op = reader.GetInt32(reader.GetOrdinal("Header_op")),
                        Header_t = reader.IsDBNull(reader.GetOrdinal("Header_t")) ? null : reader.GetString(reader.GetOrdinal("Header_t")),
                        Header_DagCborObject = DagCborObject.FromBytes((byte[])reader["Header_DagCborObject"]),
                        Body_DagCborObject = DagCborObject.FromBytes((byte[])reader["Body_DagCborObject"])
                    };
                    events.Add(firehoseEvent);
                }
            }
        }

        return events;
    }



    public void DeleteAllFirehoseEvents()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM FirehoseEvent
            ";
            command.ExecuteNonQuery();
        }
    }


    public void HideFirehoseEvent(long sequenceNumber)
    {
        int newSequenceNumber = -1 * (int)sequenceNumber;

        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
UPDATE FirehoseEvent
SET SequenceNumber = @NewSequenceNumber
WHERE SequenceNumber = @SequenceNumber
            ";
            command.Parameters.AddWithValue("@NewSequenceNumber", newSequenceNumber);
            command.Parameters.AddWithValue("@SequenceNumber", sequenceNumber);
            command.ExecuteNonQuery();
        }
    }

    #endregion


    #region LOGLEVEL

    public static void CreateTable_LogLevel(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: LogLevel");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS LogLevel (
Level TEXT NOT NULL
);
        ";
        
        command.ExecuteNonQuery();        
    }

    public string GetLogLevel()
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT Level
FROM LogLevel
LIMIT 1
            ";
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    return reader.GetString(reader.GetOrdinal("Level"));
                }
            }
        }
        return "info"; // default log level
    }

    public int GetLogLevelCount()
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(*)
FROM LogLevel
            ";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public void SetLogLevel(string level)
    {
        if (GetLogLevelCount() > 0)
        {
            DeleteLogLevel();
        }

        InsertLogLevel(level);
    }

    public void InsertLogLevel(string level)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO LogLevel (Level)
VALUES (@Level)
            ";
            command.Parameters.AddWithValue("@Level", level);
            command.ExecuteNonQuery();
        }
    }

    public void UpdateLogLevel(string level)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
UPDATE LogLevel
SET Level = @Level
            ";
            command.Parameters.AddWithValue("@Level", level);
            command.ExecuteNonQuery();
        }
    }

    public void DeleteLogLevel()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM LogLevel
            ";
            command.ExecuteNonQuery();
        }
    }

    #endregion




    #region MST ITEM

    public static void CreateTable_MstItem(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: MstItem");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS MstItem (
Key TEXT PRIMARY KEY NOT NULL,
Value TEXT NOT NULL
);
        ";
        
        command.ExecuteNonQuery();        
    }

    public bool MstItemExists(string key)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(*)
FROM MstItem
WHERE Key = @Key
            ";
            command.Parameters.AddWithValue("@Key", key);
            var count = Convert.ToInt32(command.ExecuteScalar());
            return count > 0;
        }
    }


    public void InsertMstItem(string key, string value)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO MstItem (Key, Value)
VALUES (@Key, @Value)
            ";
            command.Parameters.AddWithValue("@Key", key);
            command.Parameters.AddWithValue("@Value", value);
            command.ExecuteNonQuery();
        }
    }

    public void UpdateMstItem(string key, string value)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
UPDATE MstItem
SET Value = @Value
WHERE Key = @Key
            ";
            command.Parameters.AddWithValue("@Key", key);
            command.Parameters.AddWithValue("@Value", value);
            command.ExecuteNonQuery();
        }
    }

    public void DeleteMstItem(string key)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM MstItem
WHERE Key = @Key
            ";
            command.Parameters.AddWithValue("@Key", key);
            command.ExecuteNonQuery();
        }
    }

    public void DeleteAllMstItems()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM MstItem
            ";
            command.ExecuteNonQuery();
        }
    }

    public List<MstItem> GetAllMstItems()
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT Key, Value
FROM MstItem
            ";
            var reader = command.ExecuteReader();
            List<MstItem> items = new List<MstItem>();
            while(reader.Read())
            {
                items.Add(new MstItem()
                {
                    Key = reader.GetString(0),
                    Value = reader.GetString(1)
                });
            }
            return items;
        }
    }

    #endregion
}