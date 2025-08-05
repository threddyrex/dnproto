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
        return new HashSet<string>(new string[]{"actor"});
    }

    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"outfile"});
    }



    /// <summary>
    /// Gets user profile.
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        string actor = arguments["actor"];
        JsonNode? profile = BlueskyClient.GetProfile(actor);

        BlueskyClient.PrintJsonResponseToConsole(profile);
        JsonData.WriteJsonToFile(profile, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
    }        
}