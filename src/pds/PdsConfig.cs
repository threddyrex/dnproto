using System.Text.Json;
using dnproto.log;
using dnproto.repo;

namespace dnproto.pds;

public class PdsConfig
{
    public int Port { get; set; }

    public static PdsConfig? LoadFromFile(BaseLogger logger, string filePath)
    {
        //
        // Read JSON file
        //
        var pdsConfigJson = JsonData.ReadJsonFromFile(filePath);
        if (pdsConfigJson == null)
        {
            logger.LogError($"Failed to read PDS config file: {filePath}");
            return null;
        }

        //
        // Parse fields
        //
        string? port = JsonData.SelectString(pdsConfigJson, "port");

        if(string.IsNullOrEmpty(port) || int.TryParse(port, out _) == false)
        {
            logger.LogError("PDS config file is missing 'port' field.");
            return null;
        }


        //
        // Assuming we got this far, return the config
        //
        return new PdsConfig()
        {
            Port = int.Parse(port)
        };
    }
}
