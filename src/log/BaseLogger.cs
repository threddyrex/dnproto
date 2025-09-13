
namespace dnproto.log;

public abstract class BaseLogger
{
    public int LogLevel = 1; // default to info; caller can change it

    public abstract void LogTrace(string? message); // 0
    public abstract void LogInfo(string? message); // 1
    public abstract void LogWarning(string? message); // 2
    public abstract void LogError(string? message); // 3
}
