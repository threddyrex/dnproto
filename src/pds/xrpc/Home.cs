using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;

/// <summary>
/// Handler for the home page "/".
///
/// Serves an index.html file from the static directory if it exists,
/// otherwise returns a small default page linking to the dnproto project.
/// </summary>
public class Home : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        IncrementStatistics();

        //
        // Serve {dataDir}/static/index.html if it exists.
        //
        string indexPath = Path.Combine(Pds.LocalFileSystem.GetPath_StaticDir(), "index.html");

        if (File.Exists(indexPath))
        {
            var html = File.ReadAllText(indexPath);
            return Results.Content(html, "text/html", statusCode: 200);
        }

        //
        // Otherwise, return a small default page.
        //
        string defaultHtml =
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\">" +
            "<title>dnproto</title></head><body>" +
            "<p><a href=\"https://github.com/threddyrex/dnproto\">dnproto</a> PDS implementation</p>" +
            "</body></html>";

        return Results.Content(defaultHtml, "text/html", statusCode: 200);
    }
}
