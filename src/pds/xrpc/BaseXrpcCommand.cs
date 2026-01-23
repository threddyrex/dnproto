
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using dnproto.auth;
using dnproto.repo;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;

public abstract class BaseXrpcCommand
{
    public required Pds Pds;

    public required HttpContext HttpContext;


    #region AUTH

    /// <summary>
    /// Returns true if the client is authenticated as the PDS user.
    /// </summary>
    /// <returns></returns>
    public bool UserIsAuthenticated()
    {
        return JwtSecret.AccessJwtIsValid(GetAccessJwt(), Pds.Config.JwtSecret, Pds.Config.UserDid, validateExpiry: true);
    }

    /// <summary>
    /// Returns true if the client is authenticated as the PDS user, but the token has expired.
    /// </summary>
    /// <returns></returns>
    public bool UserIsAuthenticatedButExpired()
    {
        return JwtSecret.AccessJwtIsValid(GetAccessJwt(), Pds.Config.JwtSecret, Pds.Config.UserDid, validateExpiry: false);
    }

    /// <summary>
    /// Returns a JSON response and status code for an authentication failure.
    /// If the user's token has expired, returns a 400 status code with an "ExpiredToken" error.
    /// Otherwise, returns a 401 status code with an "Unauthorized" error.
    /// </summary>
    /// <returns></returns>
    public (JsonObject response, int statusCode) GetAuthenticationFailureResponse()
    {
        if (UserIsAuthenticatedButExpired())
        {
            return (
                new JsonObject
                {
                    ["error"] = "ExpiredToken",
                    ["message"] = "Please refresh the token."
                }, 
                400);
        }
        else
        {
            return (
                new JsonObject
                {
                    ["error"] = "Unauthorized",
                    ["message"] = "User is not authorized."
                }, 
                401);
        }
    }



    protected string? GetAccessJwt()
    {
        if(!HttpContext.Request.Headers.ContainsKey("Authorization"))
        {
            return null;
        }

        string? authHeader = HttpContext.Request.Headers["Authorization"];

        if(string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return null;
        }

        return authHeader.Substring("Bearer ".Length).Trim();
    }


    /// <summary>
    /// Returns true if the caller is the PDS admin user.
    /// </summary>
    /// <returns></returns>
    private bool CheckAdminAuth()
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

        bool verifyPassword = PasswordHasher.VerifyPassword(Pds.Config.AdminHashedPassword, password);

        if(username != "admin" || !verifyPassword)
        {
            return false;
        }

        return true;
    }



    protected bool IsOauthEnabled()
    {
        // refresh val from db
        Pds.Config.OauthIsEnabled = Pds.PdsDb.IsOauthEnabled();
        return Pds.Config.OauthIsEnabled;
    }

    #endregion


    #region REQUEST

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

    protected bool CheckRequestBodyParam(JsonNode? requestBody, string paramName, out string? value)
    {
        value = null;

        if(requestBody == null)
        {
            return false;
        }

        value = requestBody[paramName]?.ToString();

        if(string.IsNullOrEmpty(value))
        {
            return false;
        }

        return true;
    }

    protected bool CheckRequestBodyParam(JsonNode? requestBody, string paramName, out DagCborObject? dagCborObject)
    {
        dagCborObject = null;

        if(requestBody == null)
        {
            return false;
        }

        string? valueStr = requestBody[paramName]?.ToString();

        if(string.IsNullOrEmpty(valueStr))
        {
            return false;
        }

        dagCborObject = DagCborObject.FromJsonString(valueStr);

        if(dagCborObject == null)
        {
            return false;
        }
        
        return true;
    }

    protected bool CheckFormDataParam(string formData, string paramName, out string? value)
    {
        value = null;
        if(string.IsNullOrEmpty(formData))
        {
            return false;
        }

        var parsed = System.Web.HttpUtility.ParseQueryString(formData);
        value = parsed[paramName];
        return !string.IsNullOrEmpty(value);
    }

    protected string? GetQueryParameter(string paramName)
    {
        if(HttpContext.Request.Query.TryGetValue(paramName, out var value) && value.Count == 1)
        {
            return value.First();
        }
        return null;
    }




    #endregion



    #region LOG

    protected void LogConnectionInfo(HttpContext context)
    {
        try
        {
            string? ip = context.Connection.RemoteIpAddress?.ToString();
            int port = context.Connection.RemotePort;
            string? userAgent = context.Request.Headers.ContainsKey("User-Agent") ? context.Request.Headers["User-Agent"].ToString() : null;
            Pds.Logger.LogInfo($"[XRPC] ip={ip} port={port} agent={userAgent}");
        }
        catch(Exception ex)
        {
            Pds.Logger.LogInfo($"[XRPC] {ex.Message}");
        }
    }

    protected void LogRequest(HttpContext context)
    {
        foreach(var header in context.Request.Headers)
        {
            Pds.Logger.LogInfo($"[XRPC] Header: {header.Key} = {header.Value}");
        }

        string jsonBody = context.Request.Body != null ? new StreamReader(context.Request.Body).ReadToEndAsync().Result : string.Empty;
        Pds.Logger.LogInfo($"[XRPC] Body:\n {jsonBody}");
    }

    #endregion

}