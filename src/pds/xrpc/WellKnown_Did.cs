using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class WellKnown_Did : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        IncrementStatistics();

        string userDid = Pds.PdsDb.GetConfigProperty("UserDid");
        string userHandle = Pds.PdsDb.GetConfigProperty("UserHandle");
        string publicKeyMultibase = Pds.PdsDb.GetConfigProperty("UserPublicKeyMultibase");
        string pdsHostname = Pds.PdsDb.GetConfigProperty("PdsHostname");

        var didDocument = new DidDocument
        {
            Context = new[]
            {
                "https://www.w3.org/ns/did/v1",
                "https://w3id.org/security/multikey/v1",
                "https://w3id.org/security/suites/secp256k1-2019/v1"
            },
            Id = userDid,
            AlsoKnownAs = new[] { $"at://{userHandle}" },
            VerificationMethod = new[]
            {
                new VerificationMethod
                {
                    Id = $"{userDid}#atproto",
                    Type = "Multikey",
                    Controller = userDid,
                    PublicKeyMultibase = publicKeyMultibase
                }
            },
            Service = new[]
            {
                new ServiceEntry
                {
                    Id = "#atproto_pds",
                    Type = "AtprotoPersonalDataServer",
                    ServiceEndpoint = $"https://{pdsHostname}"
                }
            }
        };

        return Results.Json(didDocument, contentType: "application/json");
    }
}

public class DidDocument
{
    [JsonPropertyName("@context")]
    public string[] Context { get; set; } = Array.Empty<string>();

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("alsoKnownAs")]
    public string[] AlsoKnownAs { get; set; } = Array.Empty<string>();

    [JsonPropertyName("verificationMethod")]
    public VerificationMethod[] VerificationMethod { get; set; } = Array.Empty<VerificationMethod>();

    [JsonPropertyName("service")]
    public ServiceEntry[] Service { get; set; } = Array.Empty<ServiceEntry>();
}

public class VerificationMethod
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("controller")]
    public string Controller { get; set; } = string.Empty;

    [JsonPropertyName("publicKeyMultibase")]
    public string PublicKeyMultibase { get; set; } = string.Empty;
}

public class ServiceEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("serviceEndpoint")]
    public string ServiceEndpoint { get; set; } = string.Empty;
}
