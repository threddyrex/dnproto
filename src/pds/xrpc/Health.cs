using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;

public class Health : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        //
        // See if the code rev exists
        //
        string codeRevFilePath = Path.Combine(Pds.LocalFileSystem.GetDataDir(), "pds", "code-rev.txt");

        if(File.Exists(codeRevFilePath))
        {
            var codeRev = File.ReadAllText(codeRevFilePath).Trim();
            string version = $"dnproto {codeRev}";
            if(!string.IsNullOrEmpty(codeRev))
            {
                return Results.Json(new { version = version }, contentType: "application/json");
            }
        }

        //
        // Otherwise, return the default version from config
        //
        var health = new HealthResponse
        {
            Version = Config.Version
        };

        LogConnectionInfo(HttpContext);
        
        return Results.Json(health, contentType: "application/json");
    }
}

public class HealthResponse
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}
