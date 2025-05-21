using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.utils;

namespace dnproto.commands;

public class PlcDir_GetPdsHistory : BaseCommand
{
    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"did", "handle", "outfile"});
    }


    /// <summary>
    /// Gets pds history for did
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        string? did = null;

        if(CommandLineInterface.HasArgument(arguments, "handle"))
        {
            Console.WriteLine("Resolving handle to did.");
            did = Handle_Resolve.DoResolveHandle(arguments["handle"]);
        }
        else
        {
            did = CommandLineInterface.GetArgumentValue(arguments, "did");
        }

        if(string.IsNullOrEmpty(did))
        {
            Console.WriteLine("did is empty.");
            return;
        }

        string url = $"https://plc.directory/{did}/log/audit";

        Console.WriteLine($"did: {did}");
        Console.WriteLine($"url: {url}");

        JsonNode? response = WebServiceClient.SendRequest(url, HttpMethod.Get);

        // Loop through children json nodes
        if(response != null && response is JsonArray)
        {
            foreach(JsonNode? didDoc in response.AsArray())
            {
                string? pds = JsonData.SelectString(didDoc, ["operation", "services", "atproto_pds", "endpoint"]);
                string? createdAt = JsonData.SelectString(didDoc, "createdAt");
                Console.WriteLine($"createdAt: {createdAt}   pds: {pds}");
            }
        }
    }
}
