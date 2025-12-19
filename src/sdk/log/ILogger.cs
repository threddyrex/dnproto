
namespace dnproto.sdk.log;

/// <summary>
/// After initial setup of Logger, most code just knows
/// about this interface for logging. In case we change
/// things around in the future.
/// </summary>
public interface ILogger
{
    public void LogTrace(string? message);
    public void LogInfo(string? message);
    public void LogWarning(string? message);
    public void LogError(string? message);
}