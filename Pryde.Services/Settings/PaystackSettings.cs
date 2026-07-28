namespace Pryde.Services.Settings;

public class PaystackSettings
{
    public const string SectionName = "Paystack";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.paystack.co";
    public string SecretKey { get; set; } = string.Empty;
}
