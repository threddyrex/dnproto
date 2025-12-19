namespace dnproto.sdk.log;

/// <summary>
/// Log destination that writes to a file.
/// </summary>
public class FileLogDestination : ILogDestination, IDisposable
{
    private readonly StreamWriter _writer;
    private bool _disposed = false;

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
    public static FileLogDestination? CreateFromDataDir(string dataDir)
    {
        // Ensure log directory exists
        string logDir = Path.Combine(dataDir, "logs");
        if (!Directory.Exists(logDir))
        {
            return null;
        }

        // Create filename from current timestamp
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{timestamp}.log";
        string fullPath = Path.Combine(logDir, fileName);

        return new FileLogDestination(fullPath);
    }

    public void WriteMessage(string? message)
    {
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
}
