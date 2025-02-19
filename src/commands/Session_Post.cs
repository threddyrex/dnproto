using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.repo;
using dnproto.utils;

namespace dnproto.commands;

public class Session_Post : BaseCommand
{
    public override HashSet<string> GetRequiredArguments()
    {
        return new HashSet<string>(new string[]{"sessionFile", "text"});
    }
    public override HashSet<string> GetOptionalArguments()
    {
        return new HashSet<string>(new string[]{"skipResolve", "skipSend", "parsementions"});
    }

    /// <summary>
    /// Create post
    /// </summary>
    /// <param name="arguments"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void DoCommand(Dictionary<string, string> arguments)
    {
        //
        // Get arguments
        //
        JsonNode? session = JsonData.ReadJsonFromFile(CommandLineInterface.GetArgumentValue(arguments, "sessionFile"));
        string? accessJwt = JsonData.SelectString(session, "accessJwt");
        string? pds = JsonData.SelectString(session, "pds");
        string? did = JsonData.SelectString(session, "did");
        string? text = CommandLineInterface.GetArgumentValue(arguments, "text");
        bool? skipResolve = CommandLineInterface.GetArgumentValueWithDefault(arguments, "skipResolve", false);
        bool? skipSend = CommandLineInterface.GetArgumentValueWithDefault(arguments, "skipSend", false);
        bool? parsementions = CommandLineInterface.GetArgumentValueWithDefault(arguments, "parsementions", false);


        Console.WriteLine($"pds: {pds}");
        Console.WriteLine($"did: {did}");
        Console.WriteLine($"text: {text}");

        if (string.IsNullOrEmpty(pds) || string.IsNullOrEmpty(accessJwt) || string.IsNullOrEmpty(did))
        {
            Console.WriteLine("Session not found. Please log in.");
            return;
        }

        if(string.IsNullOrEmpty(text))
        {
            Console.WriteLine("Text is required.");
            return;
        }

        string url = $"https://{pds}/xrpc/com.atproto.repo.createRecord";
        Console.WriteLine($"url: {url}");

        //
        // Create json object for sending.
        //
        object? json = new
        {
            repo = did,
            collection = "app.bsky.feed.post",
            record = new {
                text = text,
                createdAt = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            }                    
        };

        //
        // If we're parsing mentions, recreate json object.
        //
        if(parsementions == true)
        {
            List<PostMention>? mentions = PostMention.FindPostMentions(text);

            if(mentions != null)
            {
                foreach(PostMention mention in mentions)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Mention: '{mention.Handle}'");
                    Console.WriteLine($"byteStart: {mention.ByteStart}");
                    Console.WriteLine($"byteEnd: {mention.ByteEnd}");

                    mention.Did = skipResolve == true ? "skipping resolve handle" : Handle_Resolve.DoResolveHandle(mention.Handle);

                    Console.WriteLine($"mentionDid: '{mention.Did}'"); 
                    Console.WriteLine();
                }

                json = new
                {
                    repo = did,
                    collection = "app.bsky.feed.post",
                    record = new {
                        text = text,
                        facets = mentions.Select(mention => JsonData.ConvertJsonStringToObject(
                            // This is a hack. We're creating an object that will eventually be
                            // converted to json for the request. However we can't create a C# object
                            // with a property named "$type". Thus we have to create it as a string
                            // first, convert to anonymous object, and then continue on.
                            $"{{\"$type\":\"app.bsky.richtext.facet\",\"index\":{{\"byteStart\":{mention.ByteStart},\"byteEnd\":{mention.ByteEnd}}},\"features\":[{{\"$type\":\"app.bsky.richtext.facet#mention\",\"did\":\"{mention.Did}\"}}]}}"
                        )).ToArray(),
                        createdAt = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    }                    
                };
            }
        }


        //
        // Send request
        //
        if(skipSend == true)
        {
            Console.WriteLine();
            Console.WriteLine("Skipping send.");
            Console.WriteLine();
            Console.WriteLine(JsonData.ConvertObjectToJsonString((object?)json));
            return;
        }

        JsonNode? postResult = WebServiceClient.SendRequest(url,
            HttpMethod.Post,
            accessJwt: accessJwt,
            content: new StringContent(JsonSerializer.Serialize(json))
        );



        //
        // Show result
        //
        WebServiceClient.PrintJsonResponseToConsole(postResult);
    }
}


public class PostMention
{
    public required string Handle { get; set; }
    public required int ByteStart { get; set; }
    public required int ByteEnd { get; set; }
    public string? Did { get; set; }

    /// <summary>
    /// Given an input text, find all mentions.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static List<PostMention>? FindPostMentions(string? text)
    {
        if(string.IsNullOrEmpty(text))
        {
            return null;
        }

        List<PostMention> mentions = new List<PostMention>();
        bool inMention = false;
        int byteStart = 0;
        int byteEnd = 0;

        for(int i = 0; i < text.Length; i++)
        {
            if(inMention == false && text[i] == '@')
            {
                inMention = true;
                byteStart = i;
            }
            else if(
                inMention == true 
                && (text[i] == ' ' 
                    || text[i] == '\n' 
                    || text[i] == '\t' 
                    || (text[i] == '.' && i < text.Length - 1 && text[i+1] == ' ')
                    || (text[i] == '.' && i < text.Length - 1 && text[i+1] == '\n')
                    || (text[i] == '.' && i < text.Length - 1 && text[i+1] == '\t')
                    || (text[i] == '.' && i == text.Length - 1)
                    || text[i] == '@'))
            {
                inMention = false;
                byteEnd = i;
                string mention = text.Substring(byteStart+1, byteEnd - byteStart - 1);
                mentions.Add(new PostMention { Handle = mention, ByteStart = byteStart, ByteEnd = byteEnd });
            }
        }

        if(inMention == true)
        {
            byteEnd = text.Length;
            string mention = text.Substring(byteStart+1, byteEnd - byteStart -1);
            mentions.Add(new PostMention { Handle = mention, ByteStart = byteStart, ByteEnd = byteEnd });
        }

        return mentions;
    }
}