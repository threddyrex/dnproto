using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.utils;

namespace dnproto.commands;
public class Repo_Get : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"repoFile"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"handle", "pds", "did"});
    }


    /// <summary>
    /// Downloads a user's repository.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? pds = CommandLineInterface.GetArgumentValue(arguments, "pds");
        string? did = CommandLineInterface.GetArgumentValue(arguments, "did");
        string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");
        string? repoFile = CommandLineInterface.GetArgumentValue(arguments, "repoFile");

        Console.WriteLine($"pds: {pds}");
        Console.WriteLine($"did: {did}");
        Console.WriteLine($"repoFile: {repoFile}");

        //
        // If we're resolving handle, do that now.
        //
        if(string.IsNullOrEmpty(handle) == false)
        {
            Console.WriteLine("Resolving handle to did.");
            Dictionary<string, string> handleInfo = Handle_ResolveInfo.DoResolveHandleInfo(handle);

            did = handleInfo.ContainsKey("did") ? handleInfo["did"] : null;
            pds = handleInfo.ContainsKey("pds") ? handleInfo["pds"] : null;
        }

        Console.WriteLine($"pds: {pds}");
        Console.WriteLine($"did: {did}");

        string url = $"https://{pds}/xrpc/com.atproto.sync.getRepo?did={did}";
        Console.WriteLine($"url: {url}");

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(did) || string.IsNullOrEmpty(repoFile))
        {
            Console.WriteLine("Invalid arguments.");
            return;
        }


        //
        // Call WS
        //
        WebServiceClient.SendRequest(url,
            HttpMethod.Get, 
            outputFilePath: repoFile,
            parseJsonResponse: false);

    }
}