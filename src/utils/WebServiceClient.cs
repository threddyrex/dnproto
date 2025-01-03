using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace dnproto.utils
{
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
        public static JsonNode? SendRequest(string url, HttpMethod getOrPut, string? accessJwt = null, string contentType = "application/json", StringContent? content = null, string? outputFilePath = null)
        {
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

                //
                // Send
                //
                var response = client.Send(request);


                //
                // Read response and return
                //
                if(response == null)
                {
                    Console.WriteLine("response is null.");
                    return null;
                }

                Console.WriteLine($"response status code: {response.StatusCode}");

                // If the user has specified an output file, write the response to that file.
                if (outputFilePath != null)
                {
                    Console.WriteLine($"writing to: {outputFilePath}");

                    using (var responseStream = response.Content.ReadAsStream())
                    {
                        using (var fs = new FileStream(outputFilePath, FileMode.Create))
                        {
                            responseStream.CopyTo(fs);
                        }
                    }

                    return null;
                }

                // Otherwise it's probably json and return that.
                using (var reader = new StreamReader(response.Content.ReadAsStream()))
                {
                    var responseText = reader.ReadToEnd();

                    if(string.IsNullOrEmpty(responseText))
                    {
                        return null;
                    }

                    var ret = JsonNode.Parse(responseText);
                    return ret;
                }
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
        }
    }
}