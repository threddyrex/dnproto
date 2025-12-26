using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.fs;
using dnproto.repo;
using dnproto.ws;
using dnproto.uri;

namespace dnproto.cli.commands;

public class GetProfile : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"actor"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"sessionActor"});
    }


    public static string[] GetLabelers()
    {
        return [
            "did:plc:ar7c4by46qjdydhdevvrndac"
            ,"did:plc:e4elbtctnfqocyfcml6h2lf7"
            ,"did:plc:wkoofae5uytcm7bjncmev6n6"
            ,"did:plc:d2mkddsbmnrgr3domzg5qexf"
            ,"did:plc:vfibt4bgozsdx6rnnnpha3x7"
            ,"did:plc:uyauirpjzk6le4ygqzatcwnq"
        ];
    }


    /// <summary>
    /// Gets user profile.
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
        string? sessionActor = CommandLineInterface.GetArgumentValue(arguments, "sessionActor");


        //
        // Load actor info and session
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
        var actorInfo_actor = lfs?.ResolveActorInfo(actor);
        var actorInfo_session = string.IsNullOrEmpty(sessionActor) == false ? lfs?.ResolveActorInfo(sessionActor) : null;
        SessionFile? session = string.IsNullOrEmpty(sessionActor) == false ? lfs?.LoadSession(actorInfo_session) : null;


        string? accessJwt = null;
        string? pds = null;

        if (session != null)
        {
            Logger.LogInfo($"Using session for actor {actorInfo_session?.Actor} from PDS {session?.pds}.");
            accessJwt = session?.accessJwt;
            pds = session?.pds;
        }
        else
        {
            Logger.LogInfo($"No login session found. Using unauthenticated requests.");
        }



        //
        // Get profile
        //
        JsonNode? profile = BlueskyClient.GetProfile(actor, accessJwt, pds, string.Join(',', GetLabelers()));

        BlueskyClient.PrintJsonResponseToConsole(profile);
        JsonData.WriteJsonToFile(profile, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
    }        
}