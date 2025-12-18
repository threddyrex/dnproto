
namespace dnproto.sdk.log;

public class NullLogger : BaseLogger
{
    public override void LogTrace(string? message) {}
    public override void LogInfo(string? message) {}
    public override void LogWarning(string? message) {}
    public override void LogError(string? message) {}
}
