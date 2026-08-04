namespace Pryde.Services.Settings;

public sealed class WalletTestingSettings
{
    public const string SectionName = "WalletTesting";

    public bool EnableManualFunding { get; init; }
}
