

using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ChatBskyConvo_GetLog : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        //
        // Return {logs:[]}
        //
        return Results.Json(new JsonObject
        {
            ["logs"] = new JsonArray()
        }, statusCode: 200);
    }
}