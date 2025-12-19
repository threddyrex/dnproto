

namespace dnproto.sdk.log;

public interface ILogDestination
{
    public void WriteMessage(string? message);
}
