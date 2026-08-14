using System.Text.Json.Serialization;

namespace Pryde.Services.Providers.SmileId;

public sealed record SmileIdLinkRequest(
    string Name,
    string UserId,
    string JobId,
    string Role,
    string Flow,
    IReadOnlyList<SmileIdLinkIdentityOption> IdentityOptions);

public sealed record SmileIdLinkIdentityOption(
    string Country,
    string IdType,
    string VerificationMethod);

public sealed class SmileIdLinkResponse
{
    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("ref_id")]
    public string? ReferenceId { get; set; }
}

public sealed class SmileIdJobStatusResponse
{
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("job_complete")]
    public bool JobComplete { get; set; }

    [JsonPropertyName("job_success")]
    public bool JobSuccess { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("result")]
    public SmileIdResultPayload? Result { get; set; }

    [JsonPropertyName("history")]
    public List<SmileIdResultPayload>? History { get; set; }
}

public sealed class SmileIdResultPayload
{
    [JsonPropertyName("Country")]
    public string? Country { get; set; }

    [JsonPropertyName("IDType")]
    public string? IdType { get; set; }

    [JsonPropertyName("id_type")]
    public string? IdTypeSnakeCase { get; set; }

    [JsonPropertyName("ResultCode")]
    public string? ResultCode { get; set; }

    [JsonPropertyName("result_code")]
    public string? ResultCodeSnakeCase { get; set; }

    [JsonPropertyName("ResultText")]
    public string? ResultText { get; set; }

    [JsonPropertyName("result_text")]
    public string? ResultTextSnakeCase { get; set; }

    [JsonPropertyName("SmileJobID")]
    public string? SmileJobId { get; set; }

    [JsonPropertyName("smile_job_id")]
    public string? SmileJobIdSnakeCase { get; set; }

    [JsonPropertyName("PartnerParams")]
    public SmileIdPartnerParams? PartnerParams { get; set; }

    [JsonPropertyName("partner_params")]
    public SmileIdPartnerParams? PartnerParamsSnakeCase { get; set; }
}

public sealed class SmileIdPartnerParams
{
    [JsonPropertyName("job_id")]
    public string? JobId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("job_type")]
    public object? JobType { get; set; }

    [JsonPropertyName("flow")]
    public string? Flow { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }
}

public sealed class SmileIdCallbackPayload
{
    [JsonPropertyName("Country")]
    public string? Country { get; set; }

    [JsonPropertyName("IDType")]
    public string? IdType { get; set; }

    [JsonPropertyName("id_type")]
    public string? IdTypeSnakeCase { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("ResultCode")]
    public string? ResultCode { get; set; }

    [JsonPropertyName("result_code")]
    public string? ResultCodeSnakeCase { get; set; }

    [JsonPropertyName("ResultText")]
    public string? ResultText { get; set; }

    [JsonPropertyName("result_text")]
    public string? ResultTextSnakeCase { get; set; }

    [JsonPropertyName("SmileJobID")]
    public string? SmileJobId { get; set; }

    [JsonPropertyName("smile_job_id")]
    public string? SmileJobIdSnakeCase { get; set; }

    [JsonPropertyName("PartnerParams")]
    public SmileIdPartnerParams? PartnerParams { get; set; }

    [JsonPropertyName("partner_params")]
    public SmileIdPartnerParams? PartnerParamsSnakeCase { get; set; }
}
