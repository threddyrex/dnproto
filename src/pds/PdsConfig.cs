using System.Text.Json;
using dnproto.log;
using dnproto.repo;

namespace dnproto.pds;

public class PdsConfig
{
    public required string Version { get; set; }
    public required string Host { get; set; }
    public int Port { get; set; }

    public required string Did { get; set; }

    public List<string> AvailableUserDomains { get; set; } = new List<string>();

    public bool InviteCodeRequired { get; set; } = true;

    public bool PhoneVerificationRequired { get; set; } = true;

    public string PrivacyPolicy { get; set; } = string.Empty;

    public string TermsOfService { get; set; } = string.Empty;

    public string ContactEmail { get; set; } = string.Empty;

    public string AdminPassword { get; set; } = string.Empty;

    public static PdsConfig? LoadFromFile(BaseLogger logger, string filePath)
    {
        //
        // Read JSON file
        //
        var pdsConfigJson = JsonData.ReadJsonFromFile(filePath);
        if (pdsConfigJson == null)
        {
            logger.LogError($"Failed to read PDS config file: {filePath}");
            return null;
        }


        //
        // Parse fields
        //
        string? version = JsonData.SelectString(pdsConfigJson, "version");
        string? host = JsonData.SelectString(pdsConfigJson, "host");
        string? port = JsonData.SelectString(pdsConfigJson, "port");
        string? did = JsonData.SelectString(pdsConfigJson, "did");
        List<string> availableUserDomains = JsonData.SelectStringArray(pdsConfigJson, "availableUserDomains")?.ToList() ?? new List<string>();
        bool inviteCodeRequired = true;
        string? inviteCodeRequiredStr = JsonData.SelectString(pdsConfigJson, "inviteCodeRequired");
        if(string.IsNullOrEmpty(inviteCodeRequiredStr) == false)
        {
            bool.TryParse(inviteCodeRequiredStr, out inviteCodeRequired);
        }
        bool phoneVerificationRequired = true;
        string? phoneVerificationRequiredStr = JsonData.SelectString(pdsConfigJson, "phoneVerificationRequired");
        if(string.IsNullOrEmpty(phoneVerificationRequiredStr) == false)
        {
            bool.TryParse(phoneVerificationRequiredStr, out phoneVerificationRequired);
        }
        string? privacyPolicy = JsonData.SelectString(pdsConfigJson, "privacyPolicy") ?? string.Empty;
        string? termsOfService = JsonData.SelectString(pdsConfigJson, "termsOfService") ?? string.Empty;
        string? contactEmail = JsonData.SelectString(pdsConfigJson, "contactEmail") ?? string.Empty;
        string? adminPassword = JsonData.SelectString(pdsConfigJson, "adminPassword") ?? string.Empty;

        //
        // Validate required fields
        //
        if(string.IsNullOrEmpty(version))
        {
            logger.LogError("PDS config file is missing 'version' field.");
            return null;
        }
        if(string.IsNullOrEmpty(host))
        {
            logger.LogError("PDS config file is missing 'host' field.");
            return null;
        }
        if(string.IsNullOrEmpty(port) || int.TryParse(port, out _) == false)
        {
            logger.LogError("PDS config file is missing 'port' field.");
            return null;
        }

        if(string.IsNullOrEmpty(did))
        {
            logger.LogError("PDS config file is missing 'did' field.");
            return null;
        }

        if(availableUserDomains.Count == 0)
        {
            logger.LogWarning("PDS config file has no 'availableUserDomains' specified. No user domains will be available.");
        }

        if(string.IsNullOrEmpty(inviteCodeRequiredStr))
        {
            logger.LogWarning("PDS config file is missing 'inviteCodeRequired' field. Defaulting to true.");
        }

        if(string.IsNullOrEmpty(phoneVerificationRequiredStr))
        {
            logger.LogWarning("PDS config file is missing 'phoneVerificationRequired' field. Defaulting to true.");
        }

        if(string.IsNullOrEmpty(adminPassword))
        {
            logger.LogError("PDS config file is missing 'adminPassword' field. An admin password is required.");
            return null;
        }



        //
        // Assuming we got this far, return the config
        //
        return new PdsConfig()
        {
            Version = version,
            Host = host,
            Port = int.Parse(port),
            Did = did,
            AvailableUserDomains = availableUserDomains,
            InviteCodeRequired = inviteCodeRequired,
            PhoneVerificationRequired = phoneVerificationRequired,
            PrivacyPolicy = privacyPolicy,
            TermsOfService = termsOfService,
            ContactEmail = contactEmail,
            AdminPassword = adminPassword
        };
    }
}
