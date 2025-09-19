using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class GetPlcExport : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[] { "outfile" });
    }
    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[] { "count", "after", "previousfile"});
    }


    /// <summary>
    /// Export data from plc
    /// 
    /// https://web.plc.directory/api/redoc#operation/Export
    /// 
    /// https://plc.directory/export?count=10&after=2024-12-08T20:33:04Z
    /// 
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get parameters
        //
        int count = 10;
        string after = "2020-04-26T06:19:25.508Z";

        if (CommandLineInterface.HasArgument(arguments, "count"))
        {
            Logger.LogInfo("Using count argument.");
            count = int.Parse(arguments["count"]);
        }

        if (CommandLineInterface.HasArgument(arguments, "after"))
        {
            Logger.LogInfo("Using after argument.");
            after = arguments["after"];
        }

        //
        // If the previous file is specified, loop through the lines of that file,
        // and find the last createdAt value from it. We'll start from there.
        //
        if (CommandLineInterface.HasArgument(arguments, "previousfile"))
        {
            Logger.LogInfo("Using previousfile argument.");
            string? previousFile = CommandLineInterface.GetArgumentValue(arguments, "previousfile");

            // read lines of file
            if (File.Exists(previousFile))
            {
                Logger.LogInfo($"File exists: {previousFile}");

                string[] lines = File.ReadAllLines(previousFile);
                if (lines.Length > 0)
                {
                    // last line is the last 'after' value
                    string lastLine = lines[^1].Trim();
                    JsonNode? jsonNodeLastLine = JsonNode.Parse(lastLine);
                    string? createdAt = JsonData.SelectString(jsonNodeLastLine, "createdAt");
                    if (createdAt != null)
                    {
                        after = createdAt;
                    }
                }
                else
                {
                    Logger.LogWarning($"Warning: {previousFile} is empty, using default 'after' value: {after}");
                }
            }
            else
            {
                Logger.LogWarning($"Warning: {previousFile} does not exist, using default 'after' value: {after}");
            }
        }


        //
        // Call endpoint
        //
        string url = $"https://plc.directory/export?count={count}&after={after}";

        Logger.LogInfo($"count: {count}");
        Logger.LogInfo($"after: {after}");
        Logger.LogInfo($"url: {url}");

        // Don't parse return JSON, because technically it returns "jsonlines", which is multiple lines of JSON.
        BlueskyClient.SendRequest(url, HttpMethod.Get, parseJsonResponse: false, outputFilePath: CommandLineInterface.GetArgumentValue(arguments, "outfile"));

    }
}
