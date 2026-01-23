
namespace dnproto.log;

/// <summary>
/// After initial setup of Logger, most code just knows
/// about this interface for logging. In case we change
/// things around in the future.
/// </summary>
public interface IDnProtoLogger
{
    public void LogTrace(string? message);
    public void LogInfo(string? message);
    public void LogWarning(string? message);
    public void LogError(string? message);
    public void LogException(Exception? ex);
}