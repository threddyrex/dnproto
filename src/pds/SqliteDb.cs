
using Microsoft.Data.Sqlite;


namespace dnproto.pds;


/// <summary>
/// Helper class for Sqlite database connections.
/// </summary>
public class SqliteDb
{
    /// <summary>
    /// Get a read/write connection to an existing database.
    /// </summary>
    /// <param name="dbPath"></param>
    /// <returns></returns>
    public static SqliteConnection GetConnection(string dbPath)
    {
        string connectionString = new SqliteConnectionStringBuilder {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString();

        var conn = new SqliteConnection(connectionString);
        conn.Open();

        return conn;
    }

    /// <summary>
    /// Get a read/write/create connection to a database, creating it if it does not exist.
    /// </summary>
    /// <param name="dbPath"></param>
    /// <returns></returns>
    public static SqliteConnection GetConnectionCreate(string dbPath)
    {
        string connectionString = new SqliteConnectionStringBuilder {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        var conn = new SqliteConnection(connectionString);
        conn.Open();

        return conn;
    }


    /// <summary>
    /// Get a read-only connection to an existing database.
    /// </summary>
    /// <param name="dbPath"></param>
    /// <returns></returns>
    public static SqliteConnection GetConnectionReadOnly(string dbPath)
    {
        string connectionString = new SqliteConnectionStringBuilder {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        var conn = new SqliteConnection(connectionString);
        conn.Open();

        return conn;
    }

    
}