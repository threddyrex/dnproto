
namespace dnproto.log;

public class ConsoleLogger : ILogger
{
    public int LogLevel { get; set; }

    public ConsoleLogger()
    {
        LogLevel = 1; // default to info; caller can change it
    }

    public void LogTrace(string? message)
    {
        if (LogLevel <= 0)
        {
            Console.WriteLine($"[TRACE] {message}");
        }
    }

    public void LogInfo(string? message)
    {
        if (LogLevel <= 1)
        {
            Console.WriteLine($"[INFO] {message}");
        }
    }

    public void LogWarning(string? message)
    {
        if (LogLevel <= 2)
        {
            Console.WriteLine($"[WARNING] {message}");
        }
    }

    public void LogError(string? message)
    {
        Console.WriteLine($"[ERROR] {message}");
    }
}
