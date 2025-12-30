

using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class ComAtprotoSync_GetRepo : BaseXrpcCommand
{
    public async Task<IResult> GetResponseAsync()
    {
        //
        // Get did
        //
        string? did = HttpContext.Request.Query["did"];
        if(string.IsNullOrEmpty(did))
        {
            return Results.Json(new { error = "InvalidRequest", message = "Missing did" }, statusCode: 400);
        }

        if(did.Equals(Pds.Config.UserDid, StringComparison.OrdinalIgnoreCase) == false)
        {
            return Results.Json(new { error = "NotFound", message = "Repo not found" }, statusCode: 404);
        }

        //
        // Write the MST to stream, using "application/vnd.ipld.car" content type
        //
        HttpContext.Response.ContentType = "application/vnd.ipld.car";
        await Pds.Repo.WriteToStreamAsync(HttpContext.Response.Body);
        return Results.Empty;
    }
}