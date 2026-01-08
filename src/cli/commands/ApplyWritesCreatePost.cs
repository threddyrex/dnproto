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

public class ApplyWritesCreatePost : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"actor", "text"});
    }

    /// <summary>
    /// Create post
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

        if(string.IsNullOrEmpty(text))
        {
            Logger.LogError("Text is required.");
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


        string url = $"https://{pds}/xrpc/com.atproto.repo.applyWrites";
        Logger.LogInfo($"url: {url}");

        //
        // Create json object for sending.
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
                    ["rkey"] = RecordKey.GenerateRkey(),
                    ["value"] = new JsonObject
                    {
                        ["$type"] = "app.bsky.feed.post",
                        ["text"] = text,
                        ["createdAt"] = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    }
                }
            }
        };


        //
        // Send request
        //
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