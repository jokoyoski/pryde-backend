using System.Text.Json.Serialization;

namespace Pryde.Services.Providers.Paystack;

public class PaystackTransaction
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("reference")]
    public string Reference { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("customer")]
    public PaystackCustomer? Customer { get; set; }
}

public class PaystackCustomer
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public class PaystackWebhookEvent
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public PaystackTransaction? Data { get; set; }
}

public class PaystackBank
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("active")]
    public bool? Active { get; set; }
}

public class PaystackResolvedAccount
{
    [JsonPropertyName("account_number")]
    public string AccountNumber { get; set; } = string.Empty;

    [JsonPropertyName("account_name")]
    public string AccountName { get; set; } = string.Empty;
}

public class PaystackTransferRecipient
{
    [JsonPropertyName("recipient_code")]
    public string RecipientCode { get; set; } = string.Empty;
}

public class PaystackTransferResult
{
    [JsonPropertyName("reference")]
    public string Reference { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("transfer_code")]
    public string TransferCode { get; set; } = string.Empty;
}

internal class PaystackResponse<T>
{
    [JsonPropertyName("status")]
    public bool Status { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

internal class PaystackTransferRecipientRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "nuban";

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("account_number")]
    public string AccountNumber { get; set; } = string.Empty;

    [JsonPropertyName("bank_code")]
    public string BankCode { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "NGN";
}

internal class PaystackTransferRequest
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "balance";

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("recipient")]
    public string Recipient { get; set; } = string.Empty;

    [JsonPropertyName("reference")]
    public string Reference { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "NGN";
}
