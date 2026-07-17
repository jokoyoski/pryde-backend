namespace Pryde.Services.Settings;

public class DojahSettings
{
    public const string SectionName = "Dojah";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string ShareableLink { get; set; } = string.Empty;
}
