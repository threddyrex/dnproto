using dnproto.sdk.log;
using Microsoft.Data.Sqlite;

namespace dnproto.pds.db;

public class PdsDb
{
    public required string _dataDir;
    public required BaseLogger _logger;


    /// <summary>
    /// Initializes the PDS database on disk. Checks that the folder exists (in local data dir, in the "pds/db" sub dir),
    /// creates a new SQLite file named "pds.db", and creates the InviteCodes table if it doesn't already exist.
    /// </summary>
    public static PdsDb? InitializePdsDb(string dataDir, BaseLogger logger)
    {
        // Check that the pds/db folder exists
        string dbDir = Path.Combine(dataDir, "pds", "db");
        
        if (!Directory.Exists(dbDir))
        {
            logger.LogError($"PDS database directory does not exist: {dbDir}");
            return null;
        }
        
        // Create connection string for the SQLite database
        string dbPath = Path.Combine(dbDir, "pds.db");
        string connectionString = new SqliteConnectionStringBuilder {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
        
        logger.LogInfo($"Initializing database at: {dbPath}");
        
        // Create the database and table
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            
            // Create InviteCodes table if it doesn't exist
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS InviteCodes (
                    InviteCode TEXT PRIMARY KEY NOT NULL,
                    UseCount INTEGER NOT NULL DEFAULT 0
                )
            ";
            
            command.ExecuteNonQuery();
            logger.LogInfo("InviteCodes table created or already exists.");
        }
        
        logger.LogInfo("Database initialization complete.");

        return new PdsDb
        {
            _dataDir = dataDir,
            _logger = logger
        };
    }
}