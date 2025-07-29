using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using dnproto.repo;

namespace dnproto.utils;

public class WebServiceClient
{
    /// <summary>
    /// Many calls to the Bluesky APIs follow the same pattern. This function implements that pattern.
    /// You'll see this being called in commands like "GetUnreadCount" and "ResolveHandle".
    /// If user specifies an output file, the response is written to that file.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="getOrPut"></param>
    /// <param name="accessJwt"></param>
    /// <param name="contentType"></param>
    /// <param name="content"></param>
    /// <param name="outputFilePath"></param>
    /// <returns></returns>
    public static JsonNode? SendRequest(string url, HttpMethod getOrPut, string? accessJwt = null, string contentType = "application/json", StringContent? content = null, bool parseJsonResponse = true, string? outputFilePath = null, string? acceptHeader = null)
    {
        Console.WriteLine($"SendRequest: {url}");

        using (HttpClient client = new HttpClient())
        {
            //
            // Set up request
            //
            var request = new HttpRequestMessage(getOrPut, url);

            if (content != null)
            {
                request.Content = content;
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }


            if (accessJwt != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);
            }

            if (!string.IsNullOrEmpty(acceptHeader))
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptHeader));
            }

            //
            // Send
            //
            var response = client.Send(request);

            if(response == null)
            {
                Console.WriteLine("response is null.");
                return null;
            }

            Console.WriteLine($"status code: {response.StatusCode} ({(int)response.StatusCode})");

            //
            // If user wants json, parse that.
            //
            JsonNode? jsonResponse = null;
            if(parseJsonResponse)
            {
                using (var reader = new StreamReader(response.Content.ReadAsStream()))
                {
                    var responseText = reader.ReadToEnd();

                    if(string.IsNullOrEmpty(responseText) == false)
                    {
                        jsonResponse = JsonNode.Parse(responseText);
                    }
                }
            }

            //
            // If the user has specified an output file, write the response to that file.
            //
            if (string.IsNullOrEmpty(outputFilePath) == false)
            {
                Console.WriteLine($"writing to: {outputFilePath}");

                if (parseJsonResponse)
                {
                    JsonData.WriteJsonToFile(jsonResponse, outputFilePath);
                }
                else
                {
                    using (var responseStream = response.Content.ReadAsStream())
                    {
                        using (var fs = new FileStream(outputFilePath, FileMode.Create))
                        {
                            responseStream.CopyTo(fs);
                        }
                    }
                }
            }

            return jsonResponse;
        }
    }

    /// <summary>
    /// Same as above, but just get string.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="getOrPut"></param>
    /// <param name="accessJwt"></param>
    /// <param name="contentType"></param>
    /// <param name="content"></param>
    /// <param name="outputFilePath"></param>
    /// <returns></returns>
    public static string? SendRequestEx(string url, HttpMethod getOrPut, string? accessJwt = null, string contentType = "application/json", StringContent? content = null, string? outputFilePath = null, string? acceptHeader = null)
    {
        Console.WriteLine($"SendRequest: {url}");

        using (HttpClient client = new HttpClient())
        {
            //
            // Set up request
            //
            var request = new HttpRequestMessage(getOrPut, url);

            if (content != null)
            {
                request.Content = content;
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }


            if (accessJwt != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);
            }

            if (!string.IsNullOrEmpty(acceptHeader))
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptHeader));
            }

            //
            // Send
            //
            var response = client.Send(request);

            if(response == null)
            {
                Console.WriteLine("response is null.");
                return null;
            }

            Console.WriteLine($"status code: {response.StatusCode} ({(int)response.StatusCode})");


            //
            // Get response text.
            //
            string? responseText = null;
            using (var reader = new StreamReader(response.Content.ReadAsStream()))
            {
                responseText = reader.ReadToEnd();
            }
            
            //
            // If the user has specified an output file, write the response to that file.
            //
            if (string.IsNullOrEmpty(outputFilePath) == false && !string.IsNullOrEmpty(responseText))
            {
                Console.WriteLine($"writing to: {outputFilePath}");
                File.WriteAllText(outputFilePath, responseText);
            }

            return responseText;
        }
    }

    /// <summary>
    /// Currently most of the commands just print the response to the console.
    /// </summary>
    /// <param name="response"></param>
    public static void PrintJsonResponseToConsole(JsonNode? response)
    {
        if(response == null)
        {
            Console.WriteLine("response returned null.");
            return;
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        Console.WriteLine("");
        Console.WriteLine("response:");
        Console.WriteLine(response.ToJsonString(options));
        Console.WriteLine("");
    }
}