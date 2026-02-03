using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.fs;
using dnproto.repo;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands;

public class GetRecordSync : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"url"});
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
        string? url = CommandLineInterface.GetArgumentValue(arguments, "url");


        //
        // Parse url
        //
        AtUri? atUri = AtUri.FromBskyPost(url);
        string? actor = atUri?.Authority;

        if(atUri == null || string.IsNullOrEmpty(atUri.Collection) || string.IsNullOrEmpty(atUri.Rkey) || string.IsNullOrEmpty(actor))
        {
            Logger.LogError($"Failed to parse bsky post url: {url}");
            return;
        }

 

        //
        // Load lfs
        //
        ActorInfo? actorInfo = LocalFileSystem!.ResolveActorInfo(actor);

        if (actorInfo == null)
        {
            Logger.LogError("Failed to resolve actor info.");
            return;
        }

        //
        // If we're resolving handle, do that now.
        //
        string tempFile = Path.GetTempFileName();
        Logger.LogInfo($"tempFile: {tempFile}");

        //
        // Call pds
        //
        BlueskyClient.GetRecordSync(actorInfo.Pds, actorInfo.Did, atUri.Collection, atUri.Rkey, tempFile);


        //
        // Walk repo record
        //
            //
            // Walk repo
            //
            Repo.WalkRepo(
                tempFile,
                (repoHeader) =>
                {
                    Logger.LogTrace("");
                    Logger.LogTrace($"REPO HEADER:");
                    Logger.LogTrace($"   roots: {repoHeader.RepoCommitCid?.GetBase32()}");
                    Logger.LogTrace($"   version: {repoHeader.Version}");
                    return true;
                },
                (repoRecord) =>
                {
                    string recordType = repoRecord.AtProtoType ?? "<null>";

                    string repoRecordType = "REPO RECORD (GENERIC)";
                    if(repoRecord.IsAtProtoRecord())
                    {
                        repoRecordType = "ATPROTO RECORD";
                    }
                    else if(RepoMst.IsMstNode(repoRecord))
                    {
                        repoRecordType = "MST NODE";
                    }
                    else if(repoRecord.IsRepoCommit())
                    {
                        repoRecordType = "REPO COMMIT";
                    }

                    Logger.LogTrace("");
                    Logger.LogTrace($"{repoRecordType}:");
                    Logger.LogTrace($"  cid: {repoRecord.Cid.GetBase32()}");
                    Logger.LogTrace($"  blockJson:\n {repoRecord.JsonString}");

                    // show dag cbor
                    Logger.LogTrace($"\nDAG CBOR OBJECT:\n{DagCborObject.GetRecursiveDebugString(repoRecord.DataBlock, 0)}");


                    return true;
                }
            );
    }
}