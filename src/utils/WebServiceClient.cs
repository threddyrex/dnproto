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
        public static JsonNode? SendRequest(string url, HttpMethod getOrPut, string? accessJwt = null, string contentType = "application/json", StringContent? content = null)
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
                using (var reader = new StreamReader(response.Content.ReadAsStream()))
                {
                    var responseText = reader.ReadToEnd();
                    var ret = JsonNode.Parse(responseText);
                    return ret;
                }
            }
        }
    }
}