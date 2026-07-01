using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;

/// <summary>
/// Handler for "/favicon.ico".
///
/// Serves the favicon.ico file from the static directory if it exists,
/// otherwise returns 404.
/// </summary>
public class Favicon : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        IncrementStatistics();

        //
        // Serve {dataDir}/static/favicon.ico if it exists.
        //
        string faviconPath = Path.Combine(Pds.LocalFileSystem.GetPath_StaticDir(), "favicon.ico");

        if (File.Exists(faviconPath))
        {
            var bytes = File.ReadAllBytes(faviconPath);
            return Results.Bytes(bytes, "image/x-icon");
        }

        return Results.StatusCode(404);
    }
}
