
namespace dnproto.log;

public class ConsoleLogger : BaseLogger
{
    public override void LogTrace(string? message)
    {
        if (LogLevel <= 0)
        {
            Console.WriteLine($"[TRACE] {message}");
        }
    }

    public override void LogInfo(string? message)
    {
        if (LogLevel <= 1)
        {
            Console.WriteLine($"[INFO] {message}");
        }
    }

    public override void LogWarning(string? message)
    {
        if (LogLevel <= 2)
        {
            Console.WriteLine($"[WARNING] {message}");
        }
    }

    public override void LogError(string? message)
    {
        Console.WriteLine($"[ERROR] {message}");
    }
}
