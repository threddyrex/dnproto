using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class WellKnown_AtprotoDid : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        IncrementStatistics();

        string userDid = Pds.PdsDb.GetConfigProperty("UserDid");

        return Results.Text(userDid, contentType: "text/plain");
    }
}
