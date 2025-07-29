using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.utils;

namespace dnproto.commands;

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
        Dictionary<string, string> resolveHandleInfo = DoResolveHandleInfo(handle);
        string? jsonData = JsonData.ConvertObjectToJsonString(resolveHandleInfo);


        //
        // Print response.
        //
        Console.WriteLine("");
        Console.WriteLine(jsonData);
        Console.WriteLine("");

        JsonData.WriteJsonToFile(jsonData, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
    }


    /// <summary>
    /// 
    /// Attempts the following steps:
    ///
    ///     1. Resolve handle to did (dns or http).
    ///     2. Resolve did to didDoc. (did:plc or did:web)
    ///     3. Resolve didDoc to pds.
    ///     
    /// </summary>
    /// <param name="handle"></param>
    /// <returns></returns>
    public static Dictionary<string, string> DoResolveHandleInfo(string handle)
    {
        Dictionary<string, string> ret = new Dictionary<string, string>();


        //
        // 1. Resolve handle to did (dns or http).
        //
        string? did = BlueskyUtils.ResolveHandleToDid_ViaDns(handle);

        if (string.IsNullOrEmpty(did))
        {
            did = BlueskyUtils.ResolveHandleToDid_ViaHttp(handle);
        }

        if(string.IsNullOrEmpty(did)) return ret;
        ret["did"] = did;


        //
        // 2. Resolve did to didDoc. (did:plc or did:web)
        //
        string? didDoc = null;
        if(did.StartsWith("did:plc"))
        {
            didDoc = BlueskyUtils.ResolveDidToDidDoc_DidPlc(did);
        }
        else if(did.StartsWith("did:web"))
        {
            didDoc = BlueskyUtils.ResolveDidToDidDoc_DidWeb(did);
        }
        else
        {
            Console.WriteLine($"Unsupported did type: {did}");
            return ret;
        }

        if (string.IsNullOrEmpty(didDoc)) return ret;
        ret["didDoc"] = didDoc;


        //
        // 3. Resolve didDoc to pds.
        //
        string? pds = BlueskyUtils.ResolveDidDocToPds(didDoc);

        if(string.IsNullOrEmpty(pds)) return ret;
        ret["pds"] = pds.Replace("https://", "");


        //
        // return
        //
        return ret;

    }
}