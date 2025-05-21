using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.utils;

namespace dnproto.commands;

public class Repo_GetStatus : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"handle", "did", "pds", "outfile"});
    }


    /// <summary>
    /// Gets repo status
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        string? pds = CommandLineInterface.GetArgumentValue(arguments, "pds");
        string? did = CommandLineInterface.GetArgumentValue(arguments, "did");
        string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");

        Console.WriteLine($"pds: {pds}");
        Console.WriteLine($"did: {did}");


        //
        // If we're resolving handle, do that now.
        //
        if(string.IsNullOrEmpty(handle) == false)
        {
            Console.WriteLine("Resolving handle to did.");
            Dictionary<string, string> handleInfo = Handle_ResolveInfo.DoResolveHandleInfo(handle);

            Console.WriteLine(JsonData.ConvertObjectToJsonString(handleInfo));
            Console.WriteLine();

            did = handleInfo.ContainsKey("did") ? handleInfo["did"] : null;
            pds = handleInfo.ContainsKey("pds") ? handleInfo["pds"] : null;
        }

        Console.WriteLine($"pds: {pds}");
        Console.WriteLine($"did: {did}");

        string url = $"https://{pds}/xrpc/com.atproto.sync.getRepoStatus?did={did}";
        Console.WriteLine($"url: {url}");

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did))
        {
            Console.WriteLine("Invalid arguments.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Calling getRepoStatus.");
        JsonNode? repoStatus = WebServiceClient.SendRequest(url,
            HttpMethod.Get);

        WebServiceClient.PrintJsonResponseToConsole(repoStatus);
        JsonData.WriteJsonToFile(repoStatus, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
    }
}