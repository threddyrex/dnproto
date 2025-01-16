using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.utils;

namespace dnproto.commands;

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

        string handle = arguments["handle"];
        string url = $"https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle?handle={handle}";

        Console.WriteLine($"handle: {handle}");
        Console.WriteLine($"url: {url}");

        //
        // Send request.
        //
        JsonNode? response = WebServiceClient.SendRequest(url,
            HttpMethod.Get);

        //
        // Print response.
        //
        WebServiceClient.PrintJsonResponseToConsole(response);
        JsonData.WriteJsonToFile(response, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
    }

    public static string? DoResolveHandle(string handle)
    {
        string url = $"https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle?handle={handle}";

        JsonNode? response = WebServiceClient.SendRequest(url,
            HttpMethod.Get);

        string? did = JsonData.SelectString(response, "did");

        return did;

    }
}