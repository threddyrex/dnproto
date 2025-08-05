using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.utils;

namespace dnproto.cli.commands;

public class Handle_ResolveInfo : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"handle"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"outfile"});
    }


    /// <summary>
    /// Resolves a handle to a JSON object.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments.
        //
        if(arguments.ContainsKey("handle") == false)
        {
            throw new ArgumentException("Missing required argument: handle");
        }

        string handle = arguments["handle"];
        Console.WriteLine($"handle: {handle}");

        //
        // Send request.
        //
        Dictionary<string, string> resolveHandleInfo = BlueskyUtils.ResolveHandleInfo(handle);
        string? jsonData = JsonData.ConvertObjectToJsonString(resolveHandleInfo);


        //
        // Print response.
        //
        Console.WriteLine("");
        Console.WriteLine(jsonData);
        Console.WriteLine("");

        JsonData.WriteJsonToFile(jsonData, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
    }
}