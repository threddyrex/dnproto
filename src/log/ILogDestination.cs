

namespace dnproto.sdk.log;

public interface ILogDestination
{
    public void WriteTrace(string? message);
    public void WriteInfo(string? message);
    public void WriteWarning(string? message);
    public void WriteError(string? message);
}
