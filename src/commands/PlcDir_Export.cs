using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.utils;

namespace dnproto.commands;

public class PlcDir_Export : BaseCommand
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
            Console.WriteLine("Using count argument.");
            count = int.Parse(arguments["count"]);
        }

        if (CommandLineInterface.HasArgument(arguments, "after"))
        {
            Console.WriteLine("Using after argument.");
            after = arguments["after"];
        }

        //
        // If the previous file is specified, loop through the lines of that file,
        // and find the last createdAt value from it. We'll start from there.
        //
        if (CommandLineInterface.HasArgument(arguments, "previousfile"))
        {
            Console.WriteLine("Using previousfile argument.");
            string? previousFile = CommandLineInterface.GetArgumentValue(arguments, "previousfile");

            // read lines of file
            if (File.Exists(previousFile))
            {
                Console.WriteLine($"File exists: {previousFile}");                

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
                    Console.WriteLine($"Warning: {previousFile} is empty, using default 'after' value: {after}");
                }
            }
            else
            {
                Console.WriteLine($"Warning: {previousFile} does not exist, using default 'after' value: {after}");
            }
        }


        //
        // Call endpoint
        //
        string url = $"https://plc.directory/export?count={count}&after={after}";

        Console.WriteLine($"count: {count}");
        Console.WriteLine($"after: {after}");
        Console.WriteLine($"url: {url}");

        // Don't parse return JSON, because technically it returns "jsonlines", which is multiple lines of JSON.
        WebServiceClient.SendRequest(url, HttpMethod.Get, parseJsonResponse: false, outputFilePath: CommandLineInterface.GetArgumentValue(arguments, "outfile"));

    }
}
