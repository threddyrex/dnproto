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
        return new HashSet<string>(new string[]{"did"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"pds", "outfile"});
    }


    /// <summary>
    /// Gets repo status
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        string pds = arguments.ContainsKey("pds") ? arguments["pds"] : "bsky.social";
        string did = arguments["did"];
        string url = $"https://{pds}/xrpc/com.atproto.sync.getRepoStatus?did={did}";

        Console.WriteLine($"pds: {pds}");
        Console.WriteLine($"did: {did}");
        Console.WriteLine($"url: {url}");

        JsonNode? repoStatus = WebServiceClient.SendRequest(url,
            HttpMethod.Get);

        WebServiceClient.PrintJsonResponseToConsole(repoStatus);
        JsonData.WriteJsonToFile(repoStatus, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
    }
}