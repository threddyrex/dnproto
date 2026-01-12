

using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ChatBskyConvo_ListConvos : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        //
        // Return {convos:[]}
        //
        return Results.Json(new JsonObject
        {
            ["convos"] = new JsonArray()
        }, statusCode: 200);
    }
}