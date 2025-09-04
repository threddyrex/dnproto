
namespace dnproto.log;

public interface ILogger
{
    void LogTrace(string? message); // 0
    void LogInfo(string? message); // 1
    void LogWarning(string? message); // 2
    void LogError(string? message); // 3
}
