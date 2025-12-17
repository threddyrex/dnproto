

namespace dnproto.pds.xrpc;


public class Hello : BaseXrpcCommand
{
    public override string GetResponse()
    {
        return "world";
    }
}