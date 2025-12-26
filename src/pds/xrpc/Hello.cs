

using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class Hello : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        return Results.Text("world");
    }
}