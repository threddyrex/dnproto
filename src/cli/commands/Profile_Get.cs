using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.ws;

namespace dnproto.cli.commands;

public class Profile_Get : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"handle"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"outfile", "password", "authFactorToken"});
    }


    private static string[] GetLabelers()
    {
        return [
            "did:plc:ar7c4by46qjdydhdevvrndac"
            ,"did:plc:e4elbtctnfqocyfcml6h2lf7"
            ,"did:plc:wkoofae5uytcm7bjncmev6n6"
            ,"did:plc:d2mkddsbmnrgr3domzg5qexf"
        ];
    }


    /// <summary>
    /// Gets user profile.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        string handle = arguments["handle"];

        //
        // Find session (if the user asks).
        //
        JsonNode? session = null;
        string? accessJwt = null;
        string? pds = null;

        if (CommandLineInterface.HasArgument(arguments, "password"))
        {
            string? password = CommandLineInterface.GetArgumentValue(arguments, "password");
            string? authFactorToken = CommandLineInterface.GetArgumentValue(arguments, "authFactorToken");

            session = BlueskyClient.CreateSession(handle, password, authFactorToken);

            accessJwt = JsonData.SelectString(session, "accessJwt");
            pds = JsonData.SelectString(session, "pds");
        }


        //
        // Get profile
        //
        JsonNode? profile = BlueskyClient.GetProfile(handle, accessJwt, pds, string.Join(',', GetLabelers()));

        BlueskyClient.PrintJsonResponseToConsole(profile);
        JsonData.WriteJsonToFile(profile, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
    }        
}