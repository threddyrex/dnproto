
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
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARNING] {message}");
            Console.ResetColor();
        }
    }

    public override void LogError(string? message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }
}
