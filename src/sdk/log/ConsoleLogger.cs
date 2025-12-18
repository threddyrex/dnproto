
namespace dnproto.sdk.log;

public class ConsoleLogger : BaseLogger
{
    private readonly object _consoleLock = new object();

    public override void LogTrace(string? message)
    {
        if (LogLevel <= 0)
        {
            lock (_consoleLock)
            {
                Console.WriteLine($"[TRACE] {message}");
            }
        }
    }

    public override void LogInfo(string? message)
    {
        if (LogLevel <= 1)
        {
            lock (_consoleLock)
            {
                Console.WriteLine($"[INFO] {message}");
            }
        }
    }

    public override void LogWarning(string? message)
    {
        if (LogLevel <= 2)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARNING] {message}");
                Console.ResetColor();
            }
        }
    }

    public override void LogError(string? message)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {message}");
            Console.ResetColor();
        }
    }
}
