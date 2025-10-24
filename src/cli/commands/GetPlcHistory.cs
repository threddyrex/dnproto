using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class GetPlcHistory : BaseCommand
{
    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"handle"});
    }


    /// <summary>
    /// Gets pds history for did. Check repo status on each pds (in case the account
    /// did not deactivate on a previous pds).
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Identify did to use
        //
        string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");
        string? did = null;

        if (string.IsNullOrEmpty(handle))
        {
            Logger.LogError("Please specify handle.");
            return;
        }
        else if (handle.StartsWith("did:"))
        {
            did = handle;
        }
        else
        {
            Logger.LogTrace("Resolving handle to did.");
            did = BlueskyClient.ResolveHandleToDid_ViaBlueskyApi(arguments["handle"]);
        }

        if (string.IsNullOrEmpty(did))
        {
            Logger.LogError("did is empty.");
            return;
        }

        if(did.StartsWith("did:web"))
        {
            Logger.LogError($"'{did}' is a did:web and does not contain plc info.");
            return;
        }


        //
        // Call PLC dir for history
        //
        string url = $"https://plc.directory/{did}/log/audit";

        Logger.LogTrace($"did: {did}");
        Logger.LogTrace($"url: {url}");

        JsonNode? response = BlueskyClient.SendRequest(url, HttpMethod.Get);

        BlueskyClient.LogTraceJsonResponse(response);

        //
        // Loop through children json nodes. Call pds for each, asking if it thinks the repo is still active
        //
        List<string> consoleOutput = new List<string>();
        Dictionary<string, string> pdsStatus = new Dictionary<string, string>();

        pdsStatus["https://bsky.social"] = "<na>";

        if(response != null && response is JsonArray)
        {
            foreach(JsonNode? didDoc in response.AsArray())
            {
                string? pds = JsonData.SelectString(didDoc, ["operation", "services", "atproto_pds", "endpoint"]);
                string? createdAt = JsonData.SelectString(didDoc, "createdAt");
                JsonArray? alsoKnownAs = didDoc?["operation"]?["alsoKnownAs"] as JsonArray;
                string? alsoKnownAsHandle = alsoKnownAs != null && alsoKnownAs.Count > 0 ? alsoKnownAs[0]?.ToString() : null;

                if(string.IsNullOrEmpty(pds))
                {
                    Logger.LogTrace("pds is empty.");
                    continue;
                }

                string? active = null;

                if(!pdsStatus.ContainsKey(pds))
                {
                    try
                    {
                        JsonNode? repoStatus = BlueskyClient.SendRequest($"{pds}/xrpc/com.atproto.sync.getRepoStatus?did={did}", HttpMethod.Get);
                        active = repoStatus?["active"]?.ToString();
                    }
                    catch (System.Net.Http.HttpRequestException)
                    {
                        active = "<exception>";
                    }

                    if(!string.IsNullOrEmpty(active))
                    {
                        pdsStatus[pds] = active;
                    }
                    else
                    {
                        pdsStatus[pds] = "<null>";
                    }
                }

                active = pdsStatus.ContainsKey(pds) ? pdsStatus[pds] : null;

                consoleOutput.Add($"{createdAt}  pds: {pds}, handle: {alsoKnownAsHandle}, active: {active}");
            }
        }

        //
        // Print out the results.
        //
        Logger.LogInfo($"PDS History for {did}:");
        foreach(string line in consoleOutput)
        {
            Logger.LogInfo(line);
        }
    }
}
