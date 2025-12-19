
using System.Text.Json.Nodes;
using dnproto.sdk.log;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;

public abstract class BaseXrpcCommand
{
    public required Pds Pds;

    public required HttpContext HttpContext;


    public abstract IResult GetResponse();

    /// <summary>
    /// Checks to see if the caller is the pds admin.
    /// </summary>
    /// <returns></returns>
    protected bool CheckAdminAuth()
    {
        if(!HttpContext.Request.Headers.ContainsKey("Authorization"))
        {
            return false;
        }

        string? authHeader = HttpContext.Request.Headers["Authorization"];

        if(string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic "))
        {
            return false;
        }

        string encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
        string decodedCredentials = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(encodedCredentials));

        string[] parts = decodedCredentials.Split(':', 2);
        if(parts.Length != 2)
        {
            return false;
        }

        string username = parts[0];
        string password = parts[1];

        if(username != "admin" || password != Pds.PdsConfig.AdminPassword)
        {
            return false;
        }

        return true;
    }

    protected JsonNode? GetRequestBodyAsJson()
    {
        using(StreamReader reader = new StreamReader(HttpContext.Request.Body))
        {
            string body = reader.ReadToEndAsync().Result;
            if(string.IsNullOrEmpty(body))
            {
                return null;
            }

            try
            {
                JsonNode? json = JsonNode.Parse(body);
                return json;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Helper for getting int in request body.
    /// </summary>
    /// <param name="requestBody"></param>
    /// <param name="paramName"></param>
    /// <param name="value"></param>
    /// <param name="minValue"></param>
    /// <param name="maxValue"></param>
    /// <returns></returns>
    protected bool CheckRequestBodyParamInt(JsonNode? requestBody, string paramName, out int value, int minValue = int.MinValue, int maxValue = int.MaxValue)
    {
        value = 0;

        if(requestBody == null)
        {
            return false;
        }

        string? valueStr = requestBody[paramName]?.ToString();

        if(string.IsNullOrEmpty(valueStr) || !int.TryParse(valueStr, out value))
        {
            return false;
        }

        if(value < minValue || value > maxValue)
        {
            return false;
        }

        return true;
    }
}