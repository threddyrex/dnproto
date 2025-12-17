
using dnproto.log;

namespace dnproto.pds.xrpc;

public abstract class BaseXrpcCommand
{
    public BaseLogger Logger = new NullLogger();

    public required PdsConfig PdsConfig;

    public abstract string GetResponse();

}