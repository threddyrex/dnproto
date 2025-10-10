using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class GetProfile : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"handle"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"dataDir"});
    }


    public static string[] GetLabelers()
    {
        return [
            "did:plc:ar7c4by46qjdydhdevvrndac"
            ,"did:plc:e4elbtctnfqocyfcml6h2lf7"
            ,"did:plc:wkoofae5uytcm7bjncmev6n6"
            ,"did:plc:d2mkddsbmnrgr3domzg5qexf"
            ,"did:plc:vfibt4bgozsdx6rnnnpha3x7"
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
        string? handle = CommandLineInterface.GetArgumentValue(arguments, "handle");


        //
        // Load session
        //
        LocalFileSystem? lfs = LocalFileSystem.Initialize(dataDir, Logger);
        SessionFile? session = lfs?.LoadSession(handle);

        string? accessJwt = null;
        string? pds = null;

        if(session != null)
        {
            accessJwt = session.accessJwt;
            pds = session.pds;
        }



        //
        // Get profile
        //
        JsonNode? profile = BlueskyClient.GetProfile(handle, accessJwt, pds, string.Join(',', GetLabelers()));

        BlueskyClient.PrintJsonResponseToConsole(profile);
        JsonData.WriteJsonToFile(profile, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
    }        
}