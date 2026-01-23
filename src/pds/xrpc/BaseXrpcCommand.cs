
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


    #region OAUTH TOKEN VALIDATION

    /// <summary>
    /// Returns true if the request is using a DPoP-bound OAuth access token.
    /// This checks for the presence of a DPoP header and a Bearer token with at+jwt type.
    /// </summary>
    protected bool IsOauthTokenRequest()
    {
        // Must have DPoP header for OAuth
        if (!HttpContext.Request.Headers.ContainsKey("DPoP"))
        {
            return false;
        }

        // Must have a Bearer token
        string? accessToken = GetAccessJwt();
        if (string.IsNullOrEmpty(accessToken))
        {
            return false;
        }

        // Check if the token has at+jwt type (OAuth access token)
        return IsOauthAccessToken(accessToken);
    }

    /// <summary>
    /// Checks if the given JWT is an OAuth access token (has at+jwt type header).
    /// </summary>
    protected bool IsOauthAccessToken(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return false;
        }

        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            var headerJson = System.Text.Encoding.UTF8.GetString(
                Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(parts[0]));
            var header = System.Text.Json.JsonDocument.Parse(headerJson).RootElement;

            if (header.TryGetProperty("typ", out var typElement))
            {
                return typElement.GetString() == "at+jwt";
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the DPoP header value from the request.
    /// </summary>
    protected string? GetDpopHeader()
    {
        if (HttpContext.Request.Headers.TryGetValue("DPoP", out var dpopValues))
        {
            return dpopValues.FirstOrDefault();
        }
        return null;
    }

    /// <summary>
    /// Result of OAuth access token validation.
    /// </summary>
    public class OauthValidationResult
    {
        public bool IsValid => string.IsNullOrEmpty(Error);
        public bool IsExpired { get; set; }
        public string? Error { get; set; }
        public string? Subject { get; set; }
        public string? Scope { get; set; }
        public string? ClientId { get; set; }
        public string? JwkThumbprint { get; set; }
    }

    /// <summary>
    /// Validates the OAuth access token from the request, including DPoP proof validation.
    /// </summary>
    /// <param name="httpMethod">The HTTP method of the request (e.g., "GET", "POST")</param>
    /// <param name="requestUri">The full request URI (e.g., "https://pds.example.com/xrpc/...")</param>
    /// <returns>Validation result with token claims or error</returns>
    protected OauthValidationResult ValidateOauthAccessToken(string httpMethod, string requestUri)
    {
        var result = new OauthValidationResult();

        // Get the access token
        string? accessToken = GetAccessJwt();
        if (string.IsNullOrEmpty(accessToken))
        {
            result.Error = "Missing access token";
            return result;
        }

        // Get the DPoP header
        string? dpopHeader = GetDpopHeader();
        if (string.IsNullOrEmpty(dpopHeader))
        {
            result.Error = "Missing DPoP header";
            return result;
        }

        // Validate the DPoP proof
        var dpopResult = JwtSecret.ValidateDpop(dpopHeader, httpMethod, requestUri);
        if (!dpopResult.IsValid || string.IsNullOrEmpty(dpopResult.JwkThumbprint))
        {
            result.Error = $"DPoP validation failed: {dpopResult.Error}";
            return result;
        }

        // Validate the access token and extract claims
        var tokenValidation = ValidateOauthAccessTokenInternal(accessToken, validateExpiry: true);
        if (!tokenValidation.IsValid)
        {
            // Check if it's just expired
            var expiredCheck = ValidateOauthAccessTokenInternal(accessToken, validateExpiry: false);
            if (expiredCheck.IsValid)
            {
                result.IsExpired = true;
                result.Error = "Token expired";
                result.Subject = expiredCheck.Subject;
                result.Scope = expiredCheck.Scope;
                result.ClientId = expiredCheck.ClientId;
                result.JwkThumbprint = expiredCheck.JwkThumbprint;
                return result;
            }

            result.Error = tokenValidation.Error;
            return result;
        }

        // Verify DPoP binding - the token's cnf.jkt must match the DPoP proof's JWK thumbprint
        if (tokenValidation.JwkThumbprint != dpopResult.JwkThumbprint)
        {
            result.Error = "DPoP proof key does not match token binding";
            return result;
        }

        // Verify the subject matches the PDS user
        if (tokenValidation.Subject != Pds.Config.UserDid)
        {
            result.Error = "Token subject does not match PDS user";
            return result;
        }

        // Verify a valid session exists for this DPoP key
        // The session must exist and not be expired (revoked sessions are deleted)
        if (!Pds.PdsDb.HasValidOauthSessionByDpopThumbprint(tokenValidation.JwkThumbprint!))
        {
            result.Error = "No valid OAuth session found for this token";
            return result;
        }

        result.Subject = tokenValidation.Subject;
        result.Scope = tokenValidation.Scope;
        result.ClientId = tokenValidation.ClientId;
        result.JwkThumbprint = tokenValidation.JwkThumbprint;
        return result;
    }

    /// <summary>
    /// Internal helper to validate an OAuth access token JWT.
    /// </summary>
    private OauthValidationResult ValidateOauthAccessTokenInternal(string accessToken, bool validateExpiry)
    {
        var result = new OauthValidationResult();

        try
        {
            var tokenHandler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
            var key = System.Text.Encoding.UTF8.GetBytes(Pds.Config.JwtSecret);

            var validationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = $"https://{Pds.Config.PdsHostname}",
                ValidateAudience = true,
                ValidAudience = $"https://{Pds.Config.PdsHostname}",
                ValidateLifetime = validateExpiry,
                ClockSkew = TimeSpan.Zero
            };

            var validationResult = tokenHandler.ValidateTokenAsync(accessToken, validationParameters).GetAwaiter().GetResult();

            if (!validationResult.IsValid)
            {
                result.Error = validationResult.Exception?.Message ?? "Token validation failed";
                return result;
            }

            // Verify the algorithm is HMAC SHA256
            if (validationResult.SecurityToken is Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jwt)
            {
                if (!jwt.Alg.Equals(Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    result.Error = "Invalid token algorithm";
                    return result;
                }

                // Extract claims
                result.Subject = jwt.Subject;
                result.Scope = jwt.Claims.FirstOrDefault(c => c.Type == "scope")?.Value;
                result.ClientId = jwt.Claims.FirstOrDefault(c => c.Type == "client_id")?.Value;

                // Extract cnf.jkt (JWK thumbprint binding)
                var cnfClaim = jwt.Claims.FirstOrDefault(c => c.Type == "cnf")?.Value;
                if (!string.IsNullOrEmpty(cnfClaim))
                {
                    try
                    {
                        var cnfJson = System.Text.Json.JsonDocument.Parse(cnfClaim).RootElement;
                        if (cnfJson.TryGetProperty("jkt", out var jktElement))
                        {
                            result.JwkThumbprint = jktElement.GetString();
                        }
                    }
                    catch
                    {
                        // cnf claim is not valid JSON
                    }
                }

                if (string.IsNullOrEmpty(result.JwkThumbprint))
                {
                    result.Error = "Token missing DPoP binding (cnf.jkt)";
                    return result;
                }
            }
            else
            {
                result.Error = "Invalid token type";
                return result;
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Error = $"Token validation error: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Returns true if the request has a valid OAuth access token (not expired).
    /// </summary>
    protected bool OauthUserIsAuthenticated()
    {
        string httpMethod = HttpContext.Request.Method;
        string requestUri = $"https://{Pds.Config.PdsHostname}{HttpContext.Request.Path}";
        var result = ValidateOauthAccessToken(httpMethod, requestUri);
        return result.IsValid;
    }

    /// <summary>
    /// Returns true if the request has an OAuth access token that is valid but expired.
    /// </summary>
    protected bool OauthUserIsAuthenticatedButExpired()
    {
        string httpMethod = HttpContext.Request.Method;
        string requestUri = $"https://{Pds.Config.PdsHostname}{HttpContext.Request.Path}";
        var result = ValidateOauthAccessToken(httpMethod, requestUri);
        return result.IsExpired;
    }

    /// <summary>
    /// Returns a JSON response and status code for an OAuth authentication failure.
    /// Includes proper error codes per OAuth 2.0 Bearer Token spec (RFC 6750).
    /// </summary>
    protected (JsonObject response, int statusCode) GetOauthAuthenticationFailureResponse()
    {
        string httpMethod = HttpContext.Request.Method;
        string requestUri = $"https://{Pds.Config.PdsHostname}{HttpContext.Request.Path}";
        var result = ValidateOauthAccessToken(httpMethod, requestUri);

        if (result.IsExpired)
        {
            return (
                new JsonObject
                {
                    ["error"] = "ExpiredToken",
                    ["message"] = "The access token has expired. Please refresh the token."
                },
                400);
        }
        else
        {
            return (
                new JsonObject
                {
                    ["error"] = "invalid_token",
                    ["error_description"] = result.Error ?? "The access token is invalid."
                },
                401);
        }
    }

    #endregion


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