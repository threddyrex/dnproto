using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.utils;

namespace dnproto.commands;

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
        string url = $"https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile?actor={actor}";

        Console.WriteLine($"actor: {actor}");
        Console.WriteLine($"url: {url}");

        JsonNode? profile = WebServiceClient.SendRequest(url,
            HttpMethod.Get);

        WebServiceClient.PrintJsonResponseToConsole(profile);
        JsonData.WriteJsonToFile(profile, CommandLineInterface.GetArgumentValue(arguments, "outfile"));
    }        

    public static JsonNode? DoGetProfile(string actor)
    {
        string url = $"https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile?actor={actor}";

        JsonNode? profile = WebServiceClient.SendRequest(url,
            HttpMethod.Get);

        return profile;
    }
}