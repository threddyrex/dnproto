using dnproto.fs;
using dnproto.log;
using dnproto.mst;
using dnproto.repo;
using Microsoft.Data.Sqlite;

namespace dnproto.pds;

/// <summary>
/// Entry point for the PDS db.
/// The PDS db is a local file in sqlite format.
/// </summary>
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
        string dbFilePath = lfs.GetPath_PdsDb();

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
        return SqliteDb.GetConnection(_lfs.GetPath_PdsDb());
    }

    public SqliteConnection GetConnectionCreate()
    {
        return SqliteDb.GetConnectionCreate(_lfs.GetPath_PdsDb());
    }

    public SqliteConnection GetConnectionReadOnly()
    {
        return SqliteDb.GetConnectionReadOnly(_lfs.GetPath_PdsDb());
    }

    #endregion




    #region CONFIGPROP


    public static void CreateTable_ConfigProperty(SqliteConnection connection, IDnProtoLogger logger)
    {
        //
        // ConfigProperty table
        //
        logger.LogInfo("table: ConfigProperty");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS ConfigProperty (
    Key TEXT PRIMARY KEY NOT NULL,
    Value TEXT NOT NULL
)
        ";
            
            command.ExecuteNonQuery();        
    }
    
    

    public string GetConfigProperty(string key)
    {
        using (var connection = GetConnection())
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM ConfigProperty WHERE Key = @key";
            command.Parameters.AddWithValue("@key", key);
            var result = command.ExecuteScalar();
            connection.Close();

            // throw if not exists
            if (result == null)
            {
                throw new Exception($"ConfigProperty with key '{key}' does not exist.");
            }

            var val = result.ToString();

            if (string.IsNullOrEmpty(val))
            {
                throw new Exception($"ConfigProperty with key '{key}' has a null value.");
            }

            return val;
        }
    }

    public void SetConfigProperty(string key, string value)
    {
        using (var connection = GetConnection())
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO ConfigProperty (Key, Value) VALUES (@key, @value)";
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", value);
            command.ExecuteNonQuery();
            connection.Close();
        }
    }

    public void SetConfigPropertyBool(string key, bool value)
    {
        SetConfigProperty(key, value ? "1" : "0");
    }

    public bool GetConfigPropertyBool(string key)
    {
        var val = GetConfigProperty(key);
        return val == "1";
    }

    public void SetConfigPropertyInt(string key, int value)
    {
        SetConfigProperty(key, value.ToString());
    }

    public int GetConfigPropertyInt(string key)
    {
        var val = GetConfigProperty(key);
        if(int.TryParse(val, out var result))
        {
            return result;
        }
        
        throw new Exception($"ConfigProperty with key '{key}' has a non-integer value.");
    }

    public void DeleteAllConfigProperties()
    {
        using (var connection = GetConnection())
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ConfigProperty";
            command.ExecuteNonQuery();
            connection.Close();
        }
    }

    public bool ConfigPropertyExists(string key)
    {
        using (var connection = GetConnection())
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM ConfigProperty WHERE Key = @key";
            command.Parameters.AddWithValue("@key", key);
            var result = command.ExecuteScalar();
            connection.Close();

            if (result != null && Convert.ToInt32(result) > 0)
            {
                return true;
            }
            return false;
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
ContentLength INTEGER NOT NULL
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
INSERT INTO Blob (Cid, ContentType, ContentLength)
VALUES (@Cid, @ContentType, @ContentLength)
            ";
            command.Parameters.AddWithValue("@Cid", blob.Cid);
            command.Parameters.AddWithValue("@ContentType", blob.ContentType);
            command.Parameters.AddWithValue("@ContentLength", blob.ContentLength);

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
SET ContentType = @ContentType, ContentLength = @ContentLength
WHERE Cid = @Cid
            ";
            command.Parameters.AddWithValue("@Cid", blob.Cid);
            command.Parameters.AddWithValue("@ContentType", blob.ContentType);
            command.Parameters.AddWithValue("@ContentLength", blob.ContentLength);

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
                        ContentLength = reader.GetInt32(reader.GetOrdinal("ContentLength"))
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

    public void DeleteAllBlobs()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "DELETE FROM Blob";
            command.ExecuteNonQuery();
        }
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
                        Did = this.GetConfigProperty("UserDid"),
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
        if(string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(rkey))
        {
            throw new Exception("Collection and Rkey cannot be null or empty.");
        }

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
        if(string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(rkey))
        {
            throw new Exception("Collection and Rkey cannot be null or empty.");
        }

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

    public List<MstItem> GetAllRepoRecordMstItems()
    {
        var mstItems = new List<MstItem>();
        
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT Collection,Rkey,Cid FROM RepoRecord";
            
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    string collection = reader.GetString(reader.GetOrdinal("Collection"));
                    string rKey = reader.GetString(reader.GetOrdinal("Rkey"));
                    string fullKey = $"{collection}/{rKey}";
                    string cid = reader.GetString(reader.GetOrdinal("Cid"));

                    mstItems.Add(new MstItem()
                    {
                        Key = fullKey,
                        Value = cid
                    });
                }
            }
        }
        
        return mstItems;
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


    public List<(string rkey, RepoRecord)> ListRepoRecordsByCollection(string collection, int limit = 100, string? cursor = null, bool reverse = false)
    {
        if(string.IsNullOrEmpty(cursor))
        {
            cursor = "0";
        }

        var repoRecords = new List<(string rkey, RepoRecord)>();
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();

            if(reverse == true)
            {
            command.CommandText = @"
SELECT * FROM RepoRecord
WHERE Collection = @Collection
AND Rkey > @Cursor
ORDER BY Rkey ASC
LIMIT @Limit
            ";
            }
            else
            {
            command.CommandText = @"
SELECT * FROM RepoRecord
WHERE Collection = @Collection
AND Rkey > @Cursor
ORDER BY Rkey DESC
LIMIT @Limit
            ";
            }

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

    public void DeleteOldFirehoseEvents(int numHoursLookBack = 72)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            string afterDate = FirehoseEvent.GetCreatedDateMinusHours(numHoursLookBack);
            command.CommandText = @"
DELETE FROM FirehoseEvent
WHERE CreatedDate < @AfterDate
            ";
            command.Parameters.AddWithValue("@AfterDate", afterDate);
            command.ExecuteNonQuery();
        }
    }

    public int GetCountOfOldFirehoseEvents(int numHoursLookBack = 72)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            string afterDate = FirehoseEvent.GetCreatedDateMinusHours(numHoursLookBack);
            command.CommandText = @"
SELECT COUNT(*)
FROM FirehoseEvent
WHERE CreatedDate < @AfterDate
            ";
            command.Parameters.AddWithValue("@AfterDate", afterDate);
            return Convert.ToInt32(command.ExecuteScalar());
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

    #region DATETIME

    public static string FormatDateTimeForDb(DateTimeOffset dateTime)
    {
        return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }


    #endregion


    #region OAUTHREQ

    public static void CreateTable_OauthRequest(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: OauthRequest");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS OauthRequest (
RequestUri TEXT PRIMARY KEY,
ExpiresDate TEXT NOT NULL,
Dpop TEXT NOT NULL,
Body TEXT NOT NULL,
AuthorizationCode TEXT,
AuthType TEXT
);
        ";
        
        command.ExecuteNonQuery();
    }


    public void InsertOauthRequest(OauthRequest request)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO OauthRequest (RequestUri, ExpiresDate, Dpop, Body, AuthorizationCode, AuthType)
VALUES (@RequestUri, @ExpiresDate, @Dpop, @Body, @AuthorizationCode, @AuthType)
            ";
            command.Parameters.AddWithValue("@RequestUri", request.RequestUri);
            command.Parameters.AddWithValue("@ExpiresDate", request.ExpiresDate);
            command.Parameters.AddWithValue("@Dpop", request.Dpop);
            command.Parameters.AddWithValue("@Body", request.Body);
            command.Parameters.AddWithValue("@AuthorizationCode", request.AuthorizationCode ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@AuthType", request.AuthType ?? (object)DBNull.Value);
            command.ExecuteNonQuery();
        }
    }

    public bool OauthRequestExists(string requestUri)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(1)
FROM OauthRequest
WHERE RequestUri = @RequestUri AND ExpiresDate > @RightNow
            ";
            command.Parameters.AddWithValue("@RequestUri", requestUri);
            command.Parameters.AddWithValue("@RightNow", FormatDateTimeForDb(DateTimeOffset.UtcNow));
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }
    }

    public bool OauthRequestExistsByAuthorizationCode(string authorizationCode)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(1)
FROM OauthRequest
WHERE AuthorizationCode = @AuthorizationCode AND ExpiresDate > @RightNow
            ";
            command.Parameters.AddWithValue("@AuthorizationCode", authorizationCode);
            command.Parameters.AddWithValue("@RightNow", FormatDateTimeForDb(DateTimeOffset.UtcNow));
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }
    }


    public OauthRequest GetOauthRequest(string requestUri)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT RequestUri, ExpiresDate, Dpop, Body, AuthorizationCode, AuthType
FROM OauthRequest
WHERE RequestUri = @RequestUri AND ExpiresDate > @RightNow
            ";
            command.Parameters.AddWithValue("@RequestUri", requestUri);
            command.Parameters.AddWithValue("@RightNow", FormatDateTimeForDb(DateTimeOffset.UtcNow));
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    return new OauthRequest
                    {
                        RequestUri = reader.GetString(reader.GetOrdinal("RequestUri")),
                        ExpiresDate = reader.GetString(reader.GetOrdinal("ExpiresDate")),
                        Dpop = reader.GetString(reader.GetOrdinal("Dpop")),
                        Body = reader.GetString(reader.GetOrdinal("Body")),
                        AuthorizationCode = reader.IsDBNull(reader.GetOrdinal("AuthorizationCode")) ? null : reader.GetString(reader.GetOrdinal("AuthorizationCode")),
                        AuthType = reader.IsDBNull(reader.GetOrdinal("AuthType")) ? null : reader.GetString(reader.GetOrdinal("AuthType"))
                    };
                }
            }
        }

        throw new Exception($"OauthRequest with RequestUri '{requestUri}' not found.");
    }

    public void DeleteAllOauthRequests()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM OauthRequest
            ";
            command.ExecuteNonQuery();
        }
    }

    public void DeleteOldOauthRequests()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM OauthRequest
WHERE ExpiresDate < @RightNow
            ";
            command.Parameters.AddWithValue("@RightNow", FormatDateTimeForDb(DateTimeOffset.UtcNow));
            command.ExecuteNonQuery();
        }
    }

    public void DeleteOauthRequestByAuthorizationCode(string authorizationCode)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM OauthRequest
WHERE AuthorizationCode = @AuthorizationCode
            ";
            command.Parameters.AddWithValue("@AuthorizationCode", authorizationCode);
            command.ExecuteNonQuery();
        }
    }

    public void UpdateOauthRequest(OauthRequest request)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
UPDATE OauthRequest
SET ExpiresDate = @ExpiresDate,
    Dpop = @Dpop,
    Body = @Body,
    AuthorizationCode = @AuthorizationCode,
    AuthType = @AuthType
WHERE RequestUri = @RequestUri
            ";
            command.Parameters.AddWithValue("@RequestUri", request.RequestUri);
            command.Parameters.AddWithValue("@ExpiresDate", request.ExpiresDate);
            command.Parameters.AddWithValue("@Dpop", request.Dpop);
            command.Parameters.AddWithValue("@Body", request.Body);
            command.Parameters.AddWithValue("@AuthorizationCode", request.AuthorizationCode ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@AuthType", request.AuthType ?? (object)DBNull.Value);
            command.ExecuteNonQuery();
        }
    }

    public OauthRequest GetOauthRequestByAuthorizationCode(string authorizationCode)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT RequestUri, ExpiresDate, Dpop, Body, AuthorizationCode, AuthType
FROM OauthRequest
WHERE AuthorizationCode = @AuthorizationCode AND ExpiresDate > @RightNow
            ";
            command.Parameters.AddWithValue("@AuthorizationCode", authorizationCode);
            command.Parameters.AddWithValue("@RightNow", FormatDateTimeForDb(DateTimeOffset.UtcNow));
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    return new OauthRequest
                    {
                        RequestUri = reader.GetString(reader.GetOrdinal("RequestUri")),
                        ExpiresDate = reader.GetString(reader.GetOrdinal("ExpiresDate")),
                        Dpop = reader.GetString(reader.GetOrdinal("Dpop")),
                        Body = reader.GetString(reader.GetOrdinal("Body")),
                        AuthorizationCode = reader.IsDBNull(reader.GetOrdinal("AuthorizationCode")) ? null : reader.GetString(reader.GetOrdinal("AuthorizationCode")),
                        AuthType = reader.IsDBNull(reader.GetOrdinal("AuthType")) ? null : reader.GetString(reader.GetOrdinal("AuthType"))
                    };
                }
            }
        }

        throw new Exception($"OauthRequest with AuthorizationCode '{authorizationCode}' not found.");
    }

    #endregion


    #region OAUTHSESS

    public static void CreateTable_OauthSession(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: OauthSession");
        using(var command = connection.CreateCommand())
        {
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS OauthSession
(
    SessionId TEXT PRIMARY KEY,
    ClientId TEXT NOT NULL,
    Scope TEXT NOT NULL,
    DpopJwkThumbprint TEXT NOT NULL,
    RefreshToken TEXT NOT NULL,
    RefreshTokenExpiresDate TEXT NOT NULL,
    CreatedDate TEXT NOT NULL,
    IpAddress TEXT NOT NULL,
    AuthType TEXT NOT NULL
);
            ";
            command.ExecuteNonQuery();
        }

        // Index for authentication lookups by DPoP thumbprint
        using(var command = connection.CreateCommand())
        {
            command.CommandText = @"
CREATE INDEX IF NOT EXISTS IX_OauthSession_DpopJwkThumbprint 
ON OauthSession(DpopJwkThumbprint);
            ";
            command.ExecuteNonQuery();
        }
    }

    public void InsertOauthSession(OauthSession session)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO OauthSession (SessionId, ClientId, Scope, DpopJwkThumbprint, RefreshToken, RefreshTokenExpiresDate, CreatedDate, IpAddress, AuthType)
VALUES (@SessionId, @ClientId, @Scope, @DpopJwkThumbprint, @RefreshToken, @RefreshTokenExpiresDate, @CreatedDate, @IpAddress, @AuthType)
            ";
            command.Parameters.AddWithValue("@SessionId", session.SessionId);
            command.Parameters.AddWithValue("@ClientId", session.ClientId);
            command.Parameters.AddWithValue("@Scope", session.Scope);
            command.Parameters.AddWithValue("@DpopJwkThumbprint", session.DpopJwkThumbprint);
            command.Parameters.AddWithValue("@RefreshToken", session.RefreshToken);
            command.Parameters.AddWithValue("@RefreshTokenExpiresDate", session.RefreshTokenExpiresDate);
            command.Parameters.AddWithValue("@CreatedDate", session.CreatedDate);
            command.Parameters.AddWithValue("@IpAddress", session.IpAddress);
            command.Parameters.AddWithValue("@AuthType", session.AuthType);
            command.ExecuteNonQuery();
        }
    }

    public OauthSession GetOauthSessionBySessionId(string sessionId)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT SessionId, ClientId, Scope, DpopJwkThumbprint, RefreshToken, RefreshTokenExpiresDate, CreatedDate, IpAddress, AuthType
FROM OauthSession
WHERE SessionId = @SessionId
            ";
            command.Parameters.AddWithValue("@SessionId", sessionId);
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    return new OauthSession
                    {
                        SessionId = reader.GetString(reader.GetOrdinal("SessionId")),
                        ClientId = reader.GetString(reader.GetOrdinal("ClientId")),
                        Scope = reader.GetString(reader.GetOrdinal("Scope")),
                        DpopJwkThumbprint = reader.GetString(reader.GetOrdinal("DpopJwkThumbprint")),
                        RefreshToken = reader.GetString(reader.GetOrdinal("RefreshToken")),
                        RefreshTokenExpiresDate = reader.GetString(reader.GetOrdinal("RefreshTokenExpiresDate")),
                        CreatedDate = reader.GetString(reader.GetOrdinal("CreatedDate")),
                        IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
                        AuthType = reader.GetString(reader.GetOrdinal("AuthType"))
                    };
                }
            }
        }

        throw new Exception($"OauthSession with SessionId '{sessionId}' not found.");
    }

    public bool HasOauthSessionByRefreshToken(string refreshToken)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(1)
FROM OauthSession
WHERE RefreshToken = @RefreshToken AND RefreshTokenExpiresDate > @RightNow
            ";
            command.Parameters.AddWithValue("@RefreshToken", refreshToken);
            command.Parameters.AddWithValue("@RightNow", FormatDateTimeForDb(DateTimeOffset.UtcNow));
            var result = command.ExecuteScalar();
            if(result is long count)
            {
                return count > 0;
            }
            return false;
        }
    }


    public OauthSession GetOauthSessionByRefreshToken(string refreshToken)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT SessionId, ClientId, Scope, DpopJwkThumbprint, RefreshToken, RefreshTokenExpiresDate, CreatedDate, IpAddress, AuthType
FROM OauthSession
WHERE RefreshToken = @RefreshToken AND RefreshTokenExpiresDate > @RightNow
            ";
            command.Parameters.AddWithValue("@RefreshToken", refreshToken);
            command.Parameters.AddWithValue("@RightNow", FormatDateTimeForDb(DateTimeOffset.UtcNow));
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    return new OauthSession
                    {
                        SessionId = reader.GetString(reader.GetOrdinal("SessionId")),
                        ClientId = reader.GetString(reader.GetOrdinal("ClientId")),
                        Scope = reader.GetString(reader.GetOrdinal("Scope")),
                        DpopJwkThumbprint = reader.GetString(reader.GetOrdinal("DpopJwkThumbprint")),
                        RefreshToken = reader.GetString(reader.GetOrdinal("RefreshToken")),
                        RefreshTokenExpiresDate = reader.GetString(reader.GetOrdinal("RefreshTokenExpiresDate")),
                        CreatedDate = reader.GetString(reader.GetOrdinal("CreatedDate")),
                        IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
                        AuthType = reader.GetString(reader.GetOrdinal("AuthType"))
                    };
                }
            }
        }

        throw new Exception($"OauthSession with RefreshToken '{refreshToken}' not found.");
    }

    public void UpdateOauthSession(OauthSession oauthSession)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
UPDATE OauthSession
SET ClientId = @ClientId,
    Scope = @Scope,
    DpopJwkThumbprint = @DpopJwkThumbprint,
    RefreshToken = @RefreshToken,
    RefreshTokenExpiresDate = @RefreshTokenExpiresDate,
    CreatedDate = @CreatedDate,
    IpAddress = @IpAddress,
    AuthType = @AuthType
WHERE SessionId = @SessionId
            ";
            command.Parameters.AddWithValue("@SessionId", oauthSession.SessionId);
            command.Parameters.AddWithValue("@ClientId", oauthSession.ClientId);
            command.Parameters.AddWithValue("@Scope", oauthSession.Scope);
            command.Parameters.AddWithValue("@DpopJwkThumbprint", oauthSession.DpopJwkThumbprint);
            command.Parameters.AddWithValue("@RefreshToken", oauthSession.RefreshToken);
            command.Parameters.AddWithValue("@RefreshTokenExpiresDate", oauthSession.RefreshTokenExpiresDate);
            command.Parameters.AddWithValue("@CreatedDate", oauthSession.CreatedDate);
            command.Parameters.AddWithValue("@IpAddress", oauthSession.IpAddress);
            command.Parameters.AddWithValue("@AuthType", oauthSession.AuthType);
            command.ExecuteNonQuery();
        }
    }


    public void DeleteOauthSessionByRefreshToken(string refreshToken)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM OauthSession
WHERE RefreshToken = @RefreshToken
            ";
            command.Parameters.AddWithValue("@RefreshToken", refreshToken);
            command.ExecuteNonQuery();
        }
    }

    public void DeleteOauthSessionBySessionId(string sessionId)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM OauthSession
WHERE SessionId = @SessionId
            ";
            command.Parameters.AddWithValue("@SessionId", sessionId);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Checks if a valid (non-expired) OAuth session exists for the given DPoP JWK thumbprint.
    /// </summary>
    public bool HasValidOauthSessionByDpopThumbprint(string dpopJwkThumbprint)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(1)
FROM OauthSession
WHERE DpopJwkThumbprint = @DpopJwkThumbprint AND RefreshTokenExpiresDate > @RightNow
            ";
            command.Parameters.AddWithValue("@DpopJwkThumbprint", dpopJwkThumbprint);
            command.Parameters.AddWithValue("@RightNow", FormatDateTimeForDb(DateTimeOffset.UtcNow));
            var result = command.ExecuteScalar();
            if(result is long count)
            {
                return count > 0;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets an OAuth session by DPoP JWK thumbprint if it exists and is not expired.
    /// Returns null if not found or expired.
    /// </summary>
    public OauthSession? GetOauthSessionByDpopThumbprint(string dpopJwkThumbprint)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT SessionId, ClientId, Scope, DpopJwkThumbprint, RefreshToken, RefreshTokenExpiresDate, CreatedDate, IpAddress, AuthType
FROM OauthSession
WHERE DpopJwkThumbprint = @DpopJwkThumbprint AND RefreshTokenExpiresDate > @RightNow
            ";
            command.Parameters.AddWithValue("@DpopJwkThumbprint", dpopJwkThumbprint);
            command.Parameters.AddWithValue("@RightNow", FormatDateTimeForDb(DateTimeOffset.UtcNow));
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    return new OauthSession
                    {
                        SessionId = reader.GetString(reader.GetOrdinal("SessionId")),
                        ClientId = reader.GetString(reader.GetOrdinal("ClientId")),
                        Scope = reader.GetString(reader.GetOrdinal("Scope")),
                        DpopJwkThumbprint = reader.GetString(reader.GetOrdinal("DpopJwkThumbprint")),
                        RefreshToken = reader.GetString(reader.GetOrdinal("RefreshToken")),
                        RefreshTokenExpiresDate = reader.GetString(reader.GetOrdinal("RefreshTokenExpiresDate")),
                        CreatedDate = reader.GetString(reader.GetOrdinal("CreatedDate")),
                        IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
                        AuthType = reader.GetString(reader.GetOrdinal("AuthType"))
                    };
                }
            }
        }
        return null;
    }


    public List<OauthSession> GetAllOauthSessions()
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT SessionId, ClientId, Scope, DpopJwkThumbprint, RefreshToken, RefreshTokenExpiresDate, CreatedDate, IpAddress, AuthType
FROM OauthSession
            ";
            var sessions = new List<OauthSession>();
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    sessions.Add(new OauthSession
                    {
                        SessionId = reader.GetString(reader.GetOrdinal("SessionId")),
                        ClientId = reader.GetString(reader.GetOrdinal("ClientId")),
                        Scope = reader.GetString(reader.GetOrdinal("Scope")),
                        DpopJwkThumbprint = reader.GetString(reader.GetOrdinal("DpopJwkThumbprint")),
                        RefreshToken = reader.GetString(reader.GetOrdinal("RefreshToken")),
                        RefreshTokenExpiresDate = reader.GetString(reader.GetOrdinal("RefreshTokenExpiresDate")),
                        CreatedDate = reader.GetString(reader.GetOrdinal("CreatedDate")),
                        IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
                        AuthType = reader.GetString(reader.GetOrdinal("AuthType"))
                    });
                }
            }
            return sessions;
        }
    }


    public void DeleteOldOauthSessions()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM OauthSession
WHERE RefreshTokenExpiresDate < @RightNow
            ";
            command.Parameters.AddWithValue("@RightNow", FormatDateTimeForDb(DateTimeOffset.UtcNow));
            command.ExecuteNonQuery();
        }
    }

    public void DeleteAllOauthSessions()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM OauthSession
            ";
            command.ExecuteNonQuery();
        }
    }

    #endregion


    
    #region LEGACY

    public static void CreateTable_LegacySession(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: LegacySession");
        using(var command = connection.CreateCommand())
        {
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS LegacySession
(
    CreatedDate TEXT NOT NULL,
    AccessJwt TEXT PRIMARY KEY,
    RefreshJwt TEXT NOT NULL,
    IpAddress TEXT NOT NULL,
    UserAgent TEXT NOT NULL
);
            ";
            command.ExecuteNonQuery();
        }
    }

    public void CreateLegacySession(LegacySession session)
    {
        DateTimeOffset createdDate = DateTimeOffset.UtcNow;
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO LegacySession (CreatedDate, AccessJwt, RefreshJwt, IpAddress, UserAgent)
VALUES (@CreatedDate, @AccessJwt, @RefreshJwt, @IpAddress, @UserAgent)
            ";
            command.Parameters.AddWithValue("@CreatedDate", session.CreatedDate);
            command.Parameters.AddWithValue("@AccessJwt", session.AccessJwt);
            command.Parameters.AddWithValue("@RefreshJwt", session.RefreshJwt);
            command.Parameters.AddWithValue("@IpAddress", session.IpAddress);
            command.Parameters.AddWithValue("@UserAgent", session.UserAgent);
            command.ExecuteNonQuery();
        }
    }


    public bool LegacySessionExistsForAccessJwt(string accessJwt)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(1)
FROM LegacySession
WHERE AccessJwt = @AccessJwt
            ";
            command.Parameters.AddWithValue("@AccessJwt", accessJwt);
            var result = command.ExecuteScalar();
            if(result is long count)
            {
                return count > 0;
            }
            return false;
        }
    }

    public bool LegacySessionExistsForRefreshJwt(string refreshJwt)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(1)
FROM LegacySession
WHERE RefreshJwt = @RefreshJwt
            ";
            command.Parameters.AddWithValue("@RefreshJwt", refreshJwt);
            var result = command.ExecuteScalar();
            if(result is long count)
            {
                return count > 0;
            }
            return false;
        }
    }


    public void DeleteLegacySessionForRefreshJwt(string refreshJwt)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM LegacySession
WHERE RefreshJwt = @RefreshJwt
            ";
            command.Parameters.AddWithValue("@RefreshJwt", refreshJwt);
            command.ExecuteNonQuery();
        }
    }


    public void DeleteAllLegacySessions()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM LegacySession
            ";
            command.ExecuteNonQuery();
        }
    }


    public List<LegacySession> GetAllLegacySessions()
    {
        var sessions = new List<LegacySession>();
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT CreatedDate, AccessJwt, RefreshJwt, IpAddress, UserAgent
FROM LegacySession
            ";
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    sessions.Add(new LegacySession
                    {
                        CreatedDate = reader.GetString(reader.GetOrdinal("CreatedDate")),
                        AccessJwt = reader.GetString(reader.GetOrdinal("AccessJwt")),
                        RefreshJwt = reader.GetString(reader.GetOrdinal("RefreshJwt")),
                        IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
                        UserAgent = reader.GetString(reader.GetOrdinal("UserAgent"))
                    });
                }
            }
        }
        
        return sessions;
    }



    #endregion


    #region ADMINSESS


    public static void CreateTable_AdminSession(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: AdminSession");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS AdminSession (
SessionId TEXT PRIMARY KEY,
CreatedDate TEXT NOT NULL,
IpAddress TEXT NOT NULL,
UserAgent TEXT NOT NULL,
AuthType TEXT NOT NULL
);
        ";
        
        command.ExecuteNonQuery();
    }


    public void InsertAdminSession(AdminSession session)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO AdminSession (SessionId, CreatedDate, IpAddress, UserAgent, AuthType)
VALUES (@SessionId, @CreatedDate, @IpAddress, @UserAgent, @AuthType)
            ";
            command.Parameters.AddWithValue("@SessionId", session.SessionId);
            command.Parameters.AddWithValue("@CreatedDate", session.CreatedDate);
            command.Parameters.AddWithValue("@IpAddress", session.IpAddress);
            command.Parameters.AddWithValue("@UserAgent", session.UserAgent);
            command.Parameters.AddWithValue("@AuthType", session.AuthType);
            command.ExecuteNonQuery();
        }
    }


    public AdminSession? GetValidAdminSession(string sessionId, string ipAddress, int timeoutMinutes = 60)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT SessionId, CreatedDate, IpAddress, UserAgent, AuthType
FROM AdminSession
WHERE SessionId = @SessionId AND IpAddress = @IpAddress AND CreatedDate > @CutoffDate
            ";
            command.Parameters.AddWithValue("@SessionId", sessionId);
            command.Parameters.AddWithValue("@IpAddress", ipAddress);
            command.Parameters.AddWithValue("@CutoffDate", FormatDateTimeForDb(DateTimeOffset.UtcNow.AddMinutes(-timeoutMinutes)));
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    return new AdminSession
                    {
                        SessionId = reader.GetString(reader.GetOrdinal("SessionId")),
                        CreatedDate = reader.GetString(reader.GetOrdinal("CreatedDate")),
                        IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
                        UserAgent = reader.GetString(reader.GetOrdinal("UserAgent")),
                        AuthType = reader.GetString(reader.GetOrdinal("AuthType"))
                    };
                }
                else
                {
                    return null;
                }
            }
        }
    }



    public List<AdminSession> GetAllAdminSessions()
    {
        var sessions = new List<AdminSession>();
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT SessionId, CreatedDate, IpAddress, UserAgent, AuthType
FROM AdminSession
            ";
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    sessions.Add(new AdminSession
                    {
                        SessionId = reader.GetString(reader.GetOrdinal("SessionId")),
                        CreatedDate = reader.GetString(reader.GetOrdinal("CreatedDate")),
                        IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
                        UserAgent = reader.GetString(reader.GetOrdinal("UserAgent")),
                        AuthType = reader.GetString(reader.GetOrdinal("AuthType"))
                    });
                }
            }
        }
        return sessions;
    }


    public void DeleteStaleAdminSessions(int timeoutMinutes = 60)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM AdminSession
WHERE CreatedDate <= @CutoffDate
            ";
            command.Parameters.AddWithValue("@CutoffDate", FormatDateTimeForDb(DateTimeOffset.UtcNow.AddMinutes(-timeoutMinutes)));
            command.ExecuteNonQuery();
        }
    }

    public void DeleteAdminSession(string sessionId)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM AdminSession
WHERE SessionId = @SessionId
            ";
            command.Parameters.AddWithValue("@SessionId", sessionId);
            command.ExecuteNonQuery();
        }
    }

    public void DeleteAllAdminSessions()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM AdminSession
            ";
            command.ExecuteNonQuery();
        }
    }


    #endregion




    #region PASSKEY


    public static void CreateTable_Passkey(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: Passkey");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS Passkey (
Name TEXT PRIMARY KEY,
CreatedDate TEXT NOT NULL,
CredentialId TEXT NOT NULL,
PublicKey TEXT NOT NULL
);
        ";
        
        command.ExecuteNonQuery();
    }

    public void InsertPasskey(Passkey passkey)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO Passkey (Name, CreatedDate, CredentialId, PublicKey)
VALUES (@Name, @CreatedDate, @CredentialId, @PublicKey)
            ";
            command.Parameters.AddWithValue("@Name", passkey.Name);
            command.Parameters.AddWithValue("@CreatedDate", passkey.CreatedDate);
            command.Parameters.AddWithValue("@CredentialId", passkey.CredentialId);
            command.Parameters.AddWithValue("@PublicKey", passkey.PublicKey);
            command.ExecuteNonQuery();
        }
    }

    public List<Passkey> GetAllPasskeys()
    {
        var passkeys = new List<Passkey>();
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT Name, CreatedDate, CredentialId, PublicKey
FROM Passkey
            ";
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    passkeys.Add(new Passkey
                    {
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        CreatedDate = reader.GetString(reader.GetOrdinal("CreatedDate")),
                        CredentialId = reader.GetString(reader.GetOrdinal("CredentialId")),
                        PublicKey = reader.GetString(reader.GetOrdinal("PublicKey"))
                    });
                }
            }
        }
        return passkeys;
    }

    public void DeleteAllPasskeys()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM Passkey
            ";
            command.ExecuteNonQuery();
        }
    }

    public Passkey GetPasskeyByCredentialId(string credentialId)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT Name, CreatedDate, CredentialId, PublicKey
FROM Passkey
WHERE CredentialId = @CredentialId
            ";
            command.Parameters.AddWithValue("@CredentialId", credentialId);
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    return new Passkey
                    {
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        CreatedDate = reader.GetString(reader.GetOrdinal("CreatedDate")),
                        CredentialId = reader.GetString(reader.GetOrdinal("CredentialId")),
                        PublicKey = reader.GetString(reader.GetOrdinal("PublicKey"))
                    };
                }
            }
        }

        throw new Exception($"Passkey with CredentialId '{credentialId}' not found.");
    }

    public void DeletePasskeyByName(string name)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM Passkey WHERE Name = @Name
            ";
            command.Parameters.AddWithValue("@Name", name);
            command.ExecuteNonQuery();
        }
    }


    #endregion

    #region PKEYCHALL


    public static void CreateTable_PasskeyChallenge(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: PasskeyChallenge");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS PasskeyChallenge (
Challenge TEXT PRIMARY KEY,
CreatedDate TEXT NOT NULL
);
        ";
        
        command.ExecuteNonQuery();
    }


    public void InsertPasskeyChallenge(PasskeyChallenge challenge)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO PasskeyChallenge (Challenge, CreatedDate)
VALUES (@Challenge, @CreatedDate)
            ";
            command.Parameters.AddWithValue("@Challenge", challenge.Challenge);
            command.Parameters.AddWithValue("@CreatedDate", challenge.CreatedDate);
            command.ExecuteNonQuery();
        }
    }


    public PasskeyChallenge? GetPasskeyChallenge(string challenge)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT Challenge, CreatedDate
FROM PasskeyChallenge
WHERE Challenge = @Challenge
            ";
            command.Parameters.AddWithValue("@Challenge", challenge);
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    return new PasskeyChallenge
                    {
                        Challenge = reader.GetString(reader.GetOrdinal("Challenge")),
                        CreatedDate = reader.GetString(reader.GetOrdinal("CreatedDate"))
                    };
                }
            }
        }

        return null;
    }

    public void DeletePasskeyChallenge(string challenge)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM PasskeyChallenge WHERE Challenge = @Challenge
            ";
            command.Parameters.AddWithValue("@Challenge", challenge);
            command.ExecuteNonQuery();
        }
    }

    public void DeleteAllPasskeyChallenges()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM PasskeyChallenge
            ";
            command.ExecuteNonQuery();
        }
    }


    public List<PasskeyChallenge> GetAllPasskeyChallenges()
    {
        var challenges = new List<PasskeyChallenge>();
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT Challenge, CreatedDate
FROM PasskeyChallenge
            ";
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    challenges.Add(new PasskeyChallenge
                    {
                        Challenge = reader.GetString(reader.GetOrdinal("Challenge")),
                        CreatedDate = reader.GetString(reader.GetOrdinal("CreatedDate"))
                    });
                }
            }
        }
        return challenges;
    }



    #endregion

    #region STATS

    public static void CreateTable_Statistic(SqliteConnection connection, IDnProtoLogger logger)
    {
        logger.LogInfo("table: Statistic");
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS Statistic (
Name TEXT NOT NULL,
IpAddress TEXT NOT NULL,
UserAgent TEXT NOT NULL,
Value INTEGER NOT NULL,
LastUpdatedDate TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_Statistic_Name_UserKey
ON Statistic (Name, IpAddress, UserAgent);
        ";

        command.ExecuteNonQuery();
    }


    public void IncrementStatistic(StatisticKey key)
    {
        if (StatisticExists(key))
        {
            long current = GetStatisticValue(key);
            UpdateStatistic(key, current + 1);
        }
        else
        {
            InsertStatistic(key, 1);
        }
    }


    public long GetStatisticValue(StatisticKey key)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT Value
FROM Statistic
WHERE Name = @Name AND IpAddress = @IpAddress AND UserAgent = @UserAgent;
            ";
            command.Parameters.AddWithValue("@Name", key.Name);
            command.Parameters.AddWithValue("@IpAddress", key.IpAddress);
            command.Parameters.AddWithValue("@UserAgent", key.UserAgent);
            var result = command.ExecuteScalar();
            if(result != null)
            {
                return Convert.ToInt32(result);
            }
            throw new Exception($"Statistic with name '{key.Name}'and IpAddress '{key.IpAddress}' and UserAgent '{key.UserAgent}' not found.");
        }
    }


    public bool StatisticExists(StatisticKey key)
    {
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(1)
FROM Statistic
WHERE Name = @Name AND IpAddress = @IpAddress AND UserAgent = @UserAgent
            ";
            command.Parameters.AddWithValue("@Name", key.Name);
            command.Parameters.AddWithValue("@IpAddress", key.IpAddress);
            command.Parameters.AddWithValue("@UserAgent", key.UserAgent);
            var result = command.ExecuteScalar();
            if(result != null && Convert.ToInt32(result) > 0)
            {
                return true;
            }
            return false;
        }
    }

    private void InsertStatistic(StatisticKey key, long value)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
INSERT INTO Statistic (Name, IpAddress, UserAgent, Value, LastUpdatedDate)
VALUES (@Name, @IpAddress, @UserAgent, @Value, @LastUpdatedDate)
            ";
            command.Parameters.AddWithValue("@Name", key.Name);
            command.Parameters.AddWithValue("@IpAddress", key.IpAddress);
            command.Parameters.AddWithValue("@UserAgent", key.UserAgent);
            command.Parameters.AddWithValue("@Value", value);
            command.Parameters.AddWithValue("@LastUpdatedDate", PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow));
            command.ExecuteNonQuery();
        }
    }

    private void UpdateStatistic(StatisticKey key, long value)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
UPDATE Statistic
SET Value = @Value, LastUpdatedDate = @LastUpdatedDate
WHERE Name = @Name AND IpAddress = @IpAddress AND UserAgent = @UserAgent
            ";
            command.Parameters.AddWithValue("@Value", value);
            command.Parameters.AddWithValue("@Name", key.Name);
            command.Parameters.AddWithValue("@IpAddress", key.IpAddress);
            command.Parameters.AddWithValue("@UserAgent", key.UserAgent);
            command.Parameters.AddWithValue("@LastUpdatedDate", PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow));
            command.ExecuteNonQuery();
        }
    }

    public void DeleteAllStatistics()
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM Statistic
            ";
            command.ExecuteNonQuery();
        }
    }

    public void DeleteOldStatistics(long hoursLookback = 24)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM Statistic WHERE LastUpdatedDate < @CutoffDate
            ";
            command.Parameters.AddWithValue("@CutoffDate", PdsDb.FormatDateTimeForDb(DateTimeOffset.UtcNow.AddHours(-hoursLookback)));
            command.ExecuteNonQuery();
        }
    }



    public void DeleteStatisticByKey(StatisticKey key)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
DELETE FROM Statistic WHERE Name = @Name AND IpAddress = @IpAddress AND UserAgent = @UserAgent
            ";
            command.Parameters.AddWithValue("@Name", key.Name);
            command.Parameters.AddWithValue("@IpAddress", key.IpAddress);
            command.Parameters.AddWithValue("@UserAgent", key.UserAgent);
            command.ExecuteNonQuery();
        }
    }


    public List<Statistic> GetAllStatistics()
    {
        var stats = new List<Statistic>();
        using(var sqlConnection = GetConnectionReadOnly())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = @"
SELECT Name, IpAddress, UserAgent, Value, LastUpdatedDate
FROM Statistic
            ";
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    stats.Add(new Statistic
                    {
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
                        UserAgent = reader.GetString(reader.GetOrdinal("UserAgent")),
                        Value = reader.GetInt64(reader.GetOrdinal("Value")),
                        LastUpdatedDate = reader.GetString(reader.GetOrdinal("LastUpdatedDate"))
                    });
                }
            }
        }
        
        return stats;
    }

    #endregion
}