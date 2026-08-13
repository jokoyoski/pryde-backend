namespace Pryde.Services.Settings;

public sealed class SmileIdSettings
{
    public const string SectionName = "SmileId";
    public const string Sandbox = "Sandbox";
    public const string Production = "Production";

    public string PartnerId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Environment { get; set; } = Sandbox;
    public string SandboxBaseUrl { get; set; } = "https://testapi.smileidentity.com/";
    public string ProductionBaseUrl { get; set; } = "https://api.smileidentity.com/";
    public string CallbackUrl { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string DataPrivacyPolicyUrl { get; set; } = string.Empty;
    public int MaximumCallbackAgeMinutes { get; set; } = 5;
    public string IdentityType { get; set; } = "NIN_V2";
}
