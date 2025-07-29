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
    ///     1. Resolve handle to did, using dns.
    ///     2. Resolve handle to did, using http.
    ///     3. Resolve did to didDoc. (did:plc or did:web)
    ///     4. Resolve didDoc to pds.
    ///     
    /// </summary>
    /// <param name="handle"></param>
    /// <returns></returns>
    public static Dictionary<string, string> DoResolveHandleInfo(string handle)
    {
        Dictionary<string, string> ret = new Dictionary<string, string>();


        //
        // 1. Resolve handle to did, using dns.
        //
        string? did = null;
        string url = $"https://cloudflare-dns.com/dns-query?name=_atproto.{handle}&type=TXT";
        ret["url_dns"] = url;
        JsonNode? response = WebServiceClient.SendRequest(url, HttpMethod.Get, acceptHeader: "application/dns-json");

        if (response != null) 
        {
            did = response["Answer"]?.AsArray()?.FirstOrDefault()?.AsObject()?["data"]?.ToString().Replace("\"", "").Replace("did=", "");
        }

        Console.WriteLine($"Resolved handle '{handle}' to DID: {did}");


        //
        // 2. Resolve handle to did, using http.
        //
        if (string.IsNullOrEmpty(did))
        {
            Console.WriteLine($"No did found in dns response, trying http resolution for handle '{handle}'");

            // If dns did not return a result, try the http endpoint
            url = $"https://{handle}/.well-known/atproto-did";
            ret["url_http"] = url;
            var responseText = WebServiceClient.SendRequestEx(url, HttpMethod.Get);

            if (responseText != null)
            {
                did = responseText;
            }
        }

        Console.WriteLine($"Resolved handle '{handle}' to DID: {did}");

        if(string.IsNullOrEmpty(did)) return ret;

        ret["did"] = did;


        //
        // 3. Resolve did to didDoc. (did:plc or did:web)
        //
        string? didDoc = null;
        if(did.StartsWith("did:plc"))
        {
            string url_plc = $"https://plc.directory/{did}";
            ret["url_plc"] = url_plc;
            response = WebServiceClient.SendRequest(url_plc,
                HttpMethod.Get);

            didDoc = JsonData.ConvertToJsonString(response);

            if(didDoc != null)
            {
                ret["didDoc"] = didDoc;
            }
        }
        else if(did.StartsWith("did:web"))
        {
            string hostname = did.Replace("did:web:", "");

            string url_didweb = $"https://{hostname}/.well-known/did.json";
            ret["url_didweb"] = url_didweb;

            response = WebServiceClient.SendRequest(url_didweb, HttpMethod.Get);

            didDoc = JsonData.ConvertToJsonString(response);

            if(didDoc != null)
            {
                ret["didDoc"] = didDoc;
            }
        }


        //
        // 4. Resolve didDoc to pds.
        //
        if (string.IsNullOrEmpty(didDoc)) return ret;

        JsonNode? didDocJson = JsonNode.Parse(didDoc);
        if(didDocJson == null) return ret;

        JsonArray? services = JsonData.SelectNode(didDocJson, ["service"]) as JsonArray;

        if (services == null || services.Count == 0) return ret;

        JsonNode? service = services[0];

        string? pds = JsonData.SelectString(service, ["serviceEndpoint"]);

        if(string.IsNullOrEmpty(pds)) return ret;

        ret["pds"] = pds.Replace("https://", "");


        //
        // return
        //
        return ret;

    }
}