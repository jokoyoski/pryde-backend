namespace Pryde.Services.Settings;

public class KycSettings
{
    public const string SectionName = "Kyc";
    public const string DefaultProvider = "Dojah";

    public string ActiveProvider { get; set; } = DefaultProvider;
}
