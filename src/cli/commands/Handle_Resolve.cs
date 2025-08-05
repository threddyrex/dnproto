using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class Handle_Resolve : BaseCommand
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

        string? did = BlueskyClient.ResolveHandleToDid_ViaBlueskyApi(arguments["handle"]);

        //
        // Print response.
        //
        string? outfile = CommandLineInterface.GetArgumentValue(arguments, "outfile");
        if(string.IsNullOrEmpty(outfile) == false)
        {
            Console.WriteLine($"Writing response to file: {outfile}");
            File.WriteAllText(outfile, did ?? "");
        }

    }

}