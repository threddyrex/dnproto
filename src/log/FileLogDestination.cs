namespace dnproto.log;

/// <summary>
/// Log destination that writes to a file.
/// </summary>
public class FileLogDestination : ILogDestination, IDisposable
{
    private StreamWriter _writer;
    private bool _disposed = false;

    private int _logLength = 0;
    private long MAX_LOG_SIZE = 10 * 1024 * 1024; // 10MB

    public string FilePath { get; }

    /// <summary>
    /// Private constructor. Use CreateFromDataDir to create an instance.
    /// </summary>
    /// <param name="filePath">The full path to the log file.</param>
    private FileLogDestination(string filePath)
    {
        FilePath = filePath;
        _writer = new StreamWriter(filePath, append: true)
        {
            AutoFlush = true
        };
    }

    /// <summary>
    /// Creates a new FileLogDestination in the dataDir's log directory
    /// with a filename based on the current timestamp.
    /// </summary>
    /// <param name="dataDir">The root data directory.</param>
    /// <returns>A new FileLogDestination instance.</returns>
    public static FileLogDestination? CreateFromDataDir(string dataDir, string commandName, string? logFileName = null)
    {
        // Ensure log directory exists
        string logDir = Path.Combine(dataDir, "logs");
        if (!Directory.Exists(logDir))
        {
            return null;
        }

        // Create filename from current timestamp
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string fileName = logFileName ?? $"{timestamp}_{commandName}.log";
        string fullPath = Path.Combine(logDir, fileName);

        return new FileLogDestination(fullPath);
    }

    public void WriteTrace(string? message)
    {
        WriteMessage(message);
    }
    public void WriteInfo(string? message)
    {
        WriteMessage(message);
    }
    public void WriteWarning(string? message)
    {
        WriteMessage(message);
    }
    public void WriteError(string? message)
    {
        WriteMessage(message);
    }

    private void WriteMessage(string? message)
    {
        _logLength += message?.Length ?? 0;

        CheckRollLog();

        if (_disposed)
        {
            return;
        }

        if (message != null)
        {
            _writer.WriteLine(message);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _writer?.Dispose();
            _disposed = true;
        }
    }

    private void CheckRollLog()
    {
        if( _logLength >= MAX_LOG_SIZE)
        {
            _writer.WriteLine(" --- Rolling log file due to size limit --- ");
            _writer.Flush();
            _writer.Dispose();
            _logLength = 0;
            File.Move(FilePath, FilePath + "." + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".bak");

            _writer = new StreamWriter(FilePath, append: true)
            {
                AutoFlush = true
            };
        }
    }
}
