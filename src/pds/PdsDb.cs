using dnproto.sdk.log;
using Microsoft.Data.Sqlite;

namespace dnproto.pds;

public class PdsDb
{
    public required string _dataDir;
    public required ILogger _logger;

    public static PdsDb? ConnectPdsDb(string dataDir, ILogger logger)
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



    /// <summary>
    /// Initializes the PDS database on disk. Checks that the folder exists (in local data dir, in the "pds/db" sub dir).
    /// It will create the database file and tables if they do not already exist.
    /// </summary>
    public static bool InitializePdsDb(PdsConfig pdsConfig, string dataDir, ILogger logger)
    {
        //
        // Check that the pds/db folder exists.
        //
        string dbDir = Path.Combine(dataDir, "pds");

        if (!Directory.Exists(dbDir))
        {
            logger.LogError($"PDS database directory does not exist: {dbDir}");
            return false;
        }


        //
        // Create connection string for the SQLite database.
        // It will create the db if it doesn't exist.
        //
        string dbPath = Path.Combine(dbDir, "pds.db");
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
            // InviteCodes table
            //
            logger.LogInfo("table: InviteCodes");
            var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS InviteCodes (
    Account TEXT NOT NULL,
    InviteCode TEXT PRIMARY KEY NOT NULL,
    UseCount INTEGER NOT NULL DEFAULT 0
)
            ";
            
            command.ExecuteNonQuery();


        }
        
        logger.LogInfo("Database initialization complete.");


        //
        // Return PdsDb instance.
        //
        return true;
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


    public string? GetAccountHashedPassword(string? did)
    {
        if (string.IsNullOrEmpty(did))
        {
            return null;
        }
        
        using(var sqlConnection = GetConnection())
        {
            var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT HashedPassword FROM Accounts WHERE Did = @did";
            command.Parameters.AddWithValue("@did", did);
            
            var result = command.ExecuteScalar();
            return result != null ? result.ToString() ?? null : null;
        }
    }
}