

using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class Hello : BaseXrpcCommand
{
    public override IResult GetResponse()
    {
        return Results.Text("world");
    }
}