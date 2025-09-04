
namespace dnproto.log;

public class NullLogger : ILogger
{
    public void LogTrace(string? message) {}
    public void LogInfo(string? message) {}
    public void LogWarning(string? message) {}
    public void LogError(string? message) {}
}
