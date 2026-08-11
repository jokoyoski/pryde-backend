namespace Pryde.Services.Settings;

public class BookingPaymentSettings
{
    public const string SectionName = "BookingPayment";

    public int PaymentWindowMinutes { get; set; } = 60;
    public int ExpiryCheckIntervalMinutes { get; set; } = 1;
}
