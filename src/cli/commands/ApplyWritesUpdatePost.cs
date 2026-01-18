using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;
using dnproto.fs;

namespace dnproto.cli.commands;

public class ApplyWritesUpdatePost : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"actor", "text", "rkey"});
    }

    /// <summary>
    /// Update a post.
    /// If you get a post by rkey, delete that post, then recreate a post using same rkey,
    /// it appears like an edit in the appview. The interaction counters (likes/etc.) will be
    /// reset though.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    [RequiresUnreferencedCode("PublishTrimmed is false")]
    [RequiresDynamicCode("PublishTrimmed is false")]
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
        string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");
        string? text = CommandLineInterface.GetArgumentValue(arguments, "text");
        string? rkey = CommandLineInterface.GetArgumentValue(arguments, "rkey");

        if(string.IsNullOrEmpty(text))
        {
            Logger.LogError("Text is required.");
            return;
        }

        if(string.IsNullOrEmpty(rkey))
        {
            Logger.LogError("Record key (rkey) is required.");
            return;
        }

        //
        // Get actor info
        //
        ActorInfo? actorInfo = LocalFileSystem?.ResolveActorInfo(actor);

        if(actorInfo is null)
        {
            Logger.LogError($"Failed to resolve actor info for actor: {actor}");
            return;
        }

        //
        // Load session
        //
        SessionFile? session = LocalFileSystem?.LoadSession(actorInfo);
        if (session == null)
        {
            Logger.LogError($"Failed to load session for actor: {actor}. Please log in first.");
            return;
        }


        string accessJwt = session.accessJwt;
        string pds = session.pds;
        string did = session.did;


        //
        // GET RECORD
        //
        string getRecordUrl = $"https://{pds}/xrpc/com.atproto.repo.getRecord?repo={did}&collection=app.bsky.feed.post&rkey={rkey}";
        Logger.LogInfo($"url: {getRecordUrl}");
        JsonNode? recordResult = BlueskyClient.SendRequest(getRecordUrl,
            HttpMethod.Get,
            accessJwt: accessJwt
        );
        if (recordResult == null)
        {
            Logger.LogError($"Failed to get record with rkey: {rkey}");
            return;
        }

        BlueskyClient.PrintJsonResponseToConsole(recordResult);

        // createdAt is at "value -> createdAt"
        string? createdAt = recordResult?["value"]?["createdAt"]?.ToString() ?? null;

        if (createdAt == null)
        {
            Logger.LogError($"Failed to get createdAt for record with rkey: {rkey}");
            return;
        }

        //
        // DELETE RECORD
        //
        string deleteRecordUrl = $"https://{pds}/xrpc/com.atproto.repo.deleteRecord";
        Logger.LogInfo($"url: {deleteRecordUrl}");
        Logger.LogInfo("\nREQUEST:\n" + JsonData.ConvertObjectToJsonString(new
        {
            repo = did,
            collection = "app.bsky.feed.post",
            rkey = rkey
        }));

        JsonNode? deleteResult = BlueskyClient.SendRequest(deleteRecordUrl,
            HttpMethod.Post,
            accessJwt: accessJwt,
            content: new StringContent(JsonSerializer.Serialize(new
            {
                repo = did,
                collection = "app.bsky.feed.post",
                rkey = rkey
            }))
        );

        BlueskyClient.PrintJsonResponseToConsole(deleteResult);


        //
        // CREATE RECORD
        //
        object? jsonObject = new JsonObject
        {
            ["repo"] = did,
            ["writes"] = new JsonArray
            {
                new JsonObject
                {
                    ["$type"] = "com.atproto.repo.applyWrites#create",
                    ["collection"] = "app.bsky.feed.post",
                    ["rkey"] = rkey,
                    ["value"] = new JsonObject
                    {
                        ["$type"] = "app.bsky.feed.post",
                        ["text"] = text,
                        ["createdAt"] = createdAt
                    }
                }
            }
        };


        //
        // Send request
        //
        string url = $"https://{pds}/xrpc/com.atproto.repo.applyWrites";
        Logger.LogInfo($"url: {url}");
        Logger.LogInfo("\nREQUEST:\n" + JsonData.ConvertObjectToJsonString((object?)jsonObject));

        JsonNode? postResult = BlueskyClient.SendRequest(url,
            HttpMethod.Post,
            accessJwt: accessJwt,
            content: new StringContent(JsonSerializer.Serialize(jsonObject))
        );



        //
        // Show result
        //
        BlueskyClient.PrintJsonResponseToConsole(postResult);
    }
}