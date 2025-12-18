using dnproto.sdk.log;
using Microsoft.Data.Sqlite;

namespace dnproto.pds.db;

public class PdsDb
{
    public required PdsConfig PdsConfig;
    public required string _dataDir;
    public required BaseLogger _logger;


    /// <summary>
    /// Initializes the PDS database on disk. Checks that the folder exists (in local data dir, in the "pds/db" sub dir).
    /// It will create the database file and tables if they do not already exist.
    /// </summary>
    public static PdsDb? InitializePdsDb(PdsConfig pdsConfig, string dataDir, BaseLogger logger)
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


        //
        // Return PdsDb instance.
        //
        return new PdsDb
        {
            PdsConfig = pdsConfig,
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


    /// <summary>
    /// Creates invite code, adds to db, and returns value.
    /// </summary>
    /// <param name="useCount"></param>
    /// <returns></returns>
    public string CreateInviteCode(int useCount)
    {
        //
        // Generate a unique invite code.
        //
        string inviteCode = PdsConfig.Host.Replace(".", "-") + "-" + Guid.NewGuid().ToString("N");
        _logger.LogTrace($"Generated invite code: {inviteCode} with use count: {useCount}");

        //
        // Insert into InviteCodes table.
        //
        ExecuteNonQuery($@"
            INSERT INTO InviteCodes (InviteCode, UseCount)
            VALUES ('{inviteCode}', {useCount})
        ");

        return inviteCode;
    }
}