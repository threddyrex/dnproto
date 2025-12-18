
using dnproto.sdk.log;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;

public abstract class BaseXrpcCommand
{
    public BaseLogger Logger = new NullLogger();

    public required PdsConfig PdsConfig;

    public required HttpContext HttpContext;

    public abstract IResult GetResponse();

}