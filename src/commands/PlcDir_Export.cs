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
        return new HashSet<string>(new string[] { "count", "after" });
    }


    /// <summary>
    /// Export data from plc
    /// 
    /// https://plc.directory/export?count=10&after=2024-12-08T20:33:04Z
    /// 
    /// https://web.plc.directory/api/redoc#operation/Export
    /// 
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
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


        string url = $"https://plc.directory/export?count={count}&after={after}";

        Console.WriteLine($"count: {count}");
        Console.WriteLine($"after: {after}");
        Console.WriteLine($"url: {url}");

        WebServiceClient.SendRequest(url, HttpMethod.Get, parseJsonResponse: false, outputFilePath: CommandLineInterface.GetArgumentValue(arguments, "outfile"));

    }
}
