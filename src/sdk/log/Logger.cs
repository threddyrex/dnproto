
namespace dnproto.sdk.log;

/// <summary>
/// Main logger class. Handles the following:
///   - log levels
///   - thread safety (locking)
///   - writing to multiple destinations (console, file, etc.)
/// </summary>
public class Logger : ILogger
{
    private int _level = 1; // default to info; caller can change it

    private List<ILogDestination> _destinations = new List<ILogDestination>();

    private readonly object _lock = new object();


    public void SetLogLevel(int level)
    {
        _level = level;
    }

    public void AddDestination(ILogDestination destination)
    {
        _destinations.Add(destination);
    }

    public void SetLogLevel(string level)
    {
        switch(level.ToLower())
        {
            case "trace":
                _level = 0;
                break;
            case "info":
                _level = 1;
                break;
            case "warning":
                _level = 2;
                break;
            default:
                _level = 1; // default to info
                break;
        }
    }

    public void LogTrace(string? message)
    {
        if (_level <= 0)
        {
            lock (_lock)
            {
                string fullMessage = $"[TRACE] {message}";

                foreach(ILogDestination destination in _destinations)
                {
                    destination.WriteMessage(fullMessage);
                }
            }
        }
    }

    public void LogInfo(string? message)
    {
        if (_level <= 1)
        {
            lock (_lock)
            {
                string fullMessage = $"[INFO] {message}";

                foreach(ILogDestination destination in _destinations)
                {
                    destination.WriteMessage(fullMessage);
                }
            }
        }
    }

    public void LogWarning(string? message)
    {
        if (_level <= 2)
        {
            lock (_lock)
            {
                string fullMessage = $"[WARNING] {message}";

                foreach(ILogDestination destination in _destinations)
                {
                    destination.WriteMessage(fullMessage);
                }
            }
        }
    }

    public void LogError(string? message)
    {
        lock (_lock)
        {
            string fullMessage = $"[ERROR] {message}";

            foreach(ILogDestination destination in _destinations)
            {
                destination.WriteMessage(fullMessage);
            }
        }
    }
}
