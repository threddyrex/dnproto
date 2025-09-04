using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

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

        //
        // Send request.
        //
        Dictionary<string, string> resolveHandleInfo = BlueskyClient.ResolveHandleInfo(handle);
        string? jsonData = JsonData.ConvertObjectToJsonString(resolveHandleInfo);


        //
        // Print response.
        //
        if (string.IsNullOrEmpty(jsonData))
        {
            Logger.LogError("Failed to resolve handle.");
        }
        else
        {
            string? pds = resolveHandleInfo.ContainsKey("pds") ? resolveHandleInfo["pds"] : "n/a";
            string? did = resolveHandleInfo.ContainsKey("did") ? resolveHandleInfo["did"] : "n/a";
            string? didDoc = resolveHandleInfo.ContainsKey("didDoc") ? resolveHandleInfo["didDoc"] : "n/a";
            Logger.LogInfo($"pds: {pds}");
            Logger.LogInfo($"did: {did}");
            Logger.LogInfo($"didDoc: {didDoc}");

            JsonData.WriteJsonToFile(jsonData, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
        }

    }
}