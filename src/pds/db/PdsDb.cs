using dnproto.log;
using Microsoft.Data.Sqlite;

namespace dnproto.pds.db;

public class PdsDb
{
    public required string _dataDir;
    public required IDnProtoLogger _logger;



    /// <summary>
    /// Initializes the PDS database on disk. Checks that the folder exists (in local data dir, in the "pds/db" sub dir).
    /// If already exists, it will fail.
    /// </summary>
    public static PdsDb? InitializePdsDb(string dataDir, IDnProtoLogger logger)
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
        if (File.Exists(dbPath))
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

            //
            // Config table
            //
            logger.LogInfo("table: Config");
            var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS Config (
    Version TEXT NOT NULL,
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

    public void ExecuteNonQuery(string sql)
    {
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
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
INSERT INTO Config (Version, ListenScheme, ListenHost, ListenPort, PdsDid, PdsHostname, AvailableUserDomain, AdminHashedPassword, JwtSecret, UserHandle, UserDid, UserHashedPassword, UserEmail, UserPublicKeyMultibase, UserPrivateKeyMultibase)
VALUES (@Version, @ListenScheme, @ListenHost, @ListenPort, @PdsDid, @PdsHostname, @AvailableUserDomain, @AdminHashedPassword, @JwtSecret, @UserHandle, @UserDid, @UserHashedPassword, @UserEmail, @UserPublicKeyMultibase, @UserPrivateKeyMultibase)
            ";
            command.Parameters.AddWithValue("@Version", config.Version);
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
        
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT * FROM Config LIMIT 1";
            
            using(var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    config.Version = reader.GetString(reader.GetOrdinal("Version"));
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
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Config";
            
            var result = command.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }
    }


    /// <summary>
    /// Executes a query and returns the result rows as a list of dictionaries.
    /// Each dictionary represents a row with column names as keys.
    /// </summary>
    /// <param name="sql">The SQL query to execute</param>
    /// <returns>List of dictionaries containing the query results</returns>
    public List<Dictionary<string, object?>> ExecuteQuery(string sql)
    {
        var results = new List<Dictionary<string, object?>>();
        
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = sql;
            using(var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    var row = new Dictionary<string, object?>();
                    for(int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    results.Add(row);
                }
            }
        }
        
        return results;
    }
}