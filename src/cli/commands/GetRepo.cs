using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.sdk.repo;
using dnproto.sdk.ws;
using dnproto.sdk.fs;

namespace dnproto.cli.commands;

public class GetRepo : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"dataDir", "actor"});
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
        string? dataDir = CommandLineInterface.GetArgumentValue(arguments, "dataDir");
        string? actor = CommandLineInterface.GetArgumentValue(arguments, "actor");

        Logger.LogTrace($"dataDir: {dataDir}");
        Logger.LogTrace($"actor: {actor}");


        //
        // Load lfs
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
        ActorInfo? actorInfo = lfs?.ResolveActorInfo(actor);

        if (actorInfo == null)
        {
            Logger.LogError("Failed to resolve actor info.");
            return;
        }

        //
        // If we're resolving handle, do that now.
        //
        Logger.LogTrace($"pds: {actorInfo.Pds}");
        Logger.LogTrace($"did: {actorInfo.Did}");

        if (string.IsNullOrEmpty(actorInfo.Pds) || string.IsNullOrEmpty(actorInfo.Did) || actorInfo.Did.StartsWith("did:") == false)
        {
            Logger.LogError("Invalid arguments.");
            return;
        }

        string? repofile = lfs?.GetPath_RepoFile(actorInfo);
        if (string.IsNullOrEmpty(repofile))
        {
            Logger.LogError("Failed to get repofile path.");
            return;
        }
        Logger.LogTrace($"repofile: {repofile}");


        //
        // Call pds
        //
        Logger.LogInfo($"Calling GetRepo.");
        Logger.LogInfo($"pds: {actorInfo.Pds}");
        Logger.LogInfo($"did: {actorInfo.Did}");
        Logger.LogInfo($"Writing repofile: {repofile}");
        BlueskyClient.GetRepo(actorInfo.Pds, actorInfo.Did, repofile);
    }
}