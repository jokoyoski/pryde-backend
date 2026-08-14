using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pryde.Services.Providers.Dojah;

internal sealed class DojahVerificationEnvelope
{
    [JsonPropertyName("entity")]
    public DojahVerificationApiResponse? Entity { get; set; }

    [JsonPropertyName("data")]
    public DojahVerificationSteps? Data { get; set; }

    [JsonPropertyName("id_url")]
    public string? IdUrl { get; set; }

    [JsonPropertyName("status")]
    public bool? Status { get; set; }

    [JsonPropertyName("id_type")]
    public string? IdType { get; set; }

    [JsonPropertyName("back_url")]
    public string? BackUrl { get; set; }

    [JsonPropertyName("selfie_url")]
    public string? SelfieUrl { get; set; }

    [JsonPropertyName("reference_id")]
    public string? ReferenceId { get; set; }

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("verification_mode")]
    public string? VerificationMode { get; set; }

    [JsonPropertyName("verification_type")]
    public string? VerificationType { get; set; }

    [JsonPropertyName("verification_status")]
    public string? VerificationStatus { get; set; }

    public DojahVerificationApiResponse? GetVerification()
    {
        if (Entity is not null)
        {
            Entity.ReferenceId ??= Entity.Reference;
            return Entity;
        }

        var referenceId = ReferenceId ?? Reference;
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            return null;
        }

        return new DojahVerificationApiResponse
        {
            Data = Data,
            IdUrl = IdUrl,
            Status = Status,
            IdType = IdType,
            BackUrl = BackUrl,
            SelfieUrl = SelfieUrl,
            ReferenceId = referenceId,
            VerificationMode = VerificationMode,
            VerificationType = VerificationType,
            VerificationStatus = VerificationStatus
        };
    }
}

internal sealed class DojahVerificationApiResponse
{
    [JsonPropertyName("data")]
    public DojahVerificationSteps? Data { get; set; }

    [JsonPropertyName("id_url")]
    public string? IdUrl { get; set; }

    [JsonPropertyName("status")]
    public bool? Status { get; set; }

    [JsonPropertyName("id_type")]
    public string? IdType { get; set; }

    [JsonPropertyName("back_url")]
    public string? BackUrl { get; set; }

    [JsonPropertyName("selfie_url")]
    public string? SelfieUrl { get; set; }

    [JsonPropertyName("reference_id")]
    public string? ReferenceId { get; set; }

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("verification_mode")]
    public string? VerificationMode { get; set; }

    [JsonPropertyName("verification_type")]
    public string? VerificationType { get; set; }

    [JsonPropertyName("verification_status")]
    public string? VerificationStatus { get; set; }
}

internal sealed class DojahVerificationSteps
{
    [JsonPropertyName("id")]
    public DojahIdStep? Id { get; set; }

    [JsonPropertyName("selfie")]
    public DojahSelfieStep? Selfie { get; set; }

    [JsonPropertyName("countries")]
    public DojahCountryStep? Countries { get; set; }

    [JsonPropertyName("user_data")]
    public DojahUserDataStep? UserData { get; set; }

    [JsonPropertyName("government_data")]
    public DojahGovernmentDataStep? GovernmentData { get; set; }
}

internal sealed class DojahIdStep
{
    [JsonPropertyName("data")]
    public DojahIdStepData? Data { get; set; }

    [JsonPropertyName("status")]
    public bool? Status { get; set; }
}

internal sealed class DojahIdStepData
{
    [JsonPropertyName("id_url")]
    public string? IdUrl { get; set; }

    [JsonPropertyName("back_url")]
    public string? BackUrl { get; set; }

    [JsonPropertyName("id_data")]
    public DojahIdentityData? IdData { get; set; }
}

internal sealed class DojahIdentityData
{
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("middle_name")]
    public string? MiddleName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("date_issued")]
    public string? DateIssued { get; set; }

    [JsonPropertyName("expiry_date")]
    public string? ExpiryDate { get; set; }

    [JsonPropertyName("date_of_birth")]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("document_type")]
    public string? DocumentType { get; set; }

    [JsonPropertyName("document_number")]
    public string? DocumentNumber { get; set; }

    [JsonPropertyName("nationality")]
    public string? Nationality { get; set; }
}

internal sealed class DojahStatusStep
{
    [JsonPropertyName("status")]
    public bool? Status { get; set; }
}

internal sealed class DojahSelfieStep
{
    [JsonPropertyName("status")]
    public bool? Status { get; set; }

    [JsonPropertyName("data")]
    public DojahSelfieData? Data { get; set; }
}

internal sealed class DojahSelfieData
{
    [JsonPropertyName("selfie_url")]
    public string? SelfieUrl { get; set; }
}

internal sealed class DojahGovernmentDataStep
{
    [JsonPropertyName("status")]
    public bool? Status { get; set; }

    [JsonPropertyName("data")]
    public DojahGovernmentData? Data { get; set; }
}

internal sealed class DojahGovernmentData
{
    [JsonPropertyName("bvn")]
    public JsonElement Bvn { get; set; }

    [JsonPropertyName("nin")]
    public JsonElement Nin { get; set; }
}

internal sealed class DojahCountryStep
{
    [JsonPropertyName("data")]
    public DojahCountryData? Data { get; set; }
}

internal sealed class DojahCountryData
{
    [JsonPropertyName("country")]
    public string? Country { get; set; }
}

internal sealed class DojahUserDataStep
{
    [JsonPropertyName("data")]
    public DojahUserData? Data { get; set; }
}

internal sealed class DojahUserData
{
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("dob")]
    public string? DateOfBirth { get; set; }
}
