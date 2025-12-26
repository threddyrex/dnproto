using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;

public class ComAtprotoServer_DescribeServer : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        var response = new DescribeServerResponse
        {
            InviteCodeRequired = true,
            PhoneVerificationRequired = true,
            AvailableUserDomains = new [] { Pds.Config.AvailableUserDomain }.ToList(),
            Did = Pds.Config.PdsDid
        };

        
        // add response header of "application/json"
        return Results.Json(response, contentType: "application/json");
    }
}

public class DescribeServerResponse
{
    [JsonPropertyName("inviteCodeRequired")]
    public bool InviteCodeRequired { get; set; }

    [JsonPropertyName("phoneVerificationRequired")]
    public bool PhoneVerificationRequired { get; set; }

    [JsonPropertyName("availableUserDomains")]
    public List<string> AvailableUserDomains { get; set; } = new List<string>();

    [JsonPropertyName("links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Links? Links { get; set; }

    [JsonPropertyName("contact")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Contact? Contact { get; set; }

    [JsonPropertyName("did")]
    public string Did { get; set; } = string.Empty;
}

public class Links
{
    [JsonPropertyName("privacyPolicy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivacyPolicy { get; set; }

    [JsonPropertyName("termsOfService")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TermsOfService { get; set; }
}

public class Contact
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}
