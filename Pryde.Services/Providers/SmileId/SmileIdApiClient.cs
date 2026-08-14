using System.Net.Http.Json;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pryde.Domain.Common.Exceptions;
using Pryde.Services.Settings;

namespace Pryde.Services.Providers.SmileId;

public sealed class SmileIdApiClient(
    HttpClient httpClient,
    IOptions<SmileIdSettings> options,
    ILogger<SmileIdApiClient> logger,
    IHostEnvironment environment,
    TimeProvider? timeProvider = null) : ISmileIdApiClient
{
    private readonly SmileIdSettings _settings = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<SmileIdLinkResponse> CreateSingleUseLinkAsync(
        SmileIdLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        var authentication = CreateAuthentication();
        using var response = await httpClient.PostAsJsonAsync(
            "v1/smile_links",
            new
            {
                partner_id = _settings.PartnerId,
                signature = authentication.Signature,
                timestamp = authentication.Timestamp,
                name = request.Name,
                company_name = _settings.CompanyName,
                id_types = request.IdentityOptions.Select(option => new
                {
                    country = option.Country,
                    id_type = option.IdType,
                    verification_method = option.VerificationMethod
                }),
                callback_url = _settings.CallbackUrl,
                data_privacy_policy_url = _settings.DataPrivacyPolicyUrl,
                redirect_url = _settings.RedirectUrl,
                is_single_use = true,
                user_id = request.UserId,
                partner_params = new Dictionary<string, string>
                {
                    ["user_id"] = request.UserId,
                    ["job_id"] = request.JobId,
                    ["flow"] = request.Flow,
                    ["role"] = request.Role
                }
            },
            JsonOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var providerError = ParseProviderError(responseBody, response.StatusCode);
            logger.LogWarning(
                "Smile ID link creation rejected. Code: {Code}; Message: {Message}; Field: {RejectedField}",
                providerError.Code,
                providerError.Message,
                providerError.RejectedField ?? "unknown");
            var message = environment.IsDevelopment()
                ? $"Smile ID rejected link creation: [{providerError.Code}] {providerError.Message}"
                : "Smile ID link creation is temporarily unavailable.";
            throw new ServiceUnavailableException(message);
        }

        var result = await response.Content.ReadFromJsonAsync<SmileIdLinkResponse>(
            JsonOptions,
            cancellationToken)
            ?? throw new ServiceUnavailableException("Smile ID returned an empty link response.");
        if (!Uri.TryCreate(result.Link, UriKind.Absolute, out var link) ||
            link.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(result.ReferenceId))
        {
            throw new ServiceUnavailableException("Smile ID returned an invalid hosted link response.");
        }

        return result;
    }

    private static SmileIdProviderError ParseProviderError(
        string? responseBody,
        System.Net.HttpStatusCode statusCode)
    {
        var fallbackCode = ((int)statusCode).ToString(CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return new SmileIdProviderError(fallbackCode, "Provider returned an empty error response.", null);
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new SmileIdProviderError(fallbackCode, "Provider returned an invalid error response.", null);
            }

            var code = GetString(root, "code", "error_code") ?? fallbackCode;
            var message = GetString(root, "error", "message", "error_message") ??
                          "Provider rejected the request.";
            var field = GetString(root, "field", "rejected_field", "parameter", "param") ??
                        InferRejectedField(message);
            return new SmileIdProviderError(
                SanitizeCode(code, fallbackCode),
                SanitizeMessage(message),
                SanitizeField(field));
        }
        catch (JsonException)
        {
            var text = responseBody.Trim();
            if (text.Length <= 200 && !text.StartsWith('{') && !text.StartsWith('['))
            {
                return new SmileIdProviderError(fallbackCode, SanitizeMessage(text), null);
            }
            return new SmileIdProviderError(fallbackCode, "Provider returned an unreadable error response.", null);
        }
    }

    private static string? GetString(JsonElement root, params string[] names)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) &&
                property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            {
                return property.Value.ToString();
            }
        }
        return null;
    }

    private static string SanitizeCode(string value, string fallback)
    {
        var sanitized = Regex.Replace(value, "[^A-Za-z0-9_.-]", string.Empty);
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized[..Math.Min(sanitized.Length, 32)];
    }

    private static string SanitizeMessage(string value)
    {
        var sanitized = Regex.Replace(value, @"https?://\S+", "[redacted-url]", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"\b[\w.%+-]+@[\w.-]+\.[A-Za-z]{2,}\b", "[redacted-email]");
        sanitized = Regex.Replace(sanitized, @"\b[0-9a-f]{8}-[0-9a-f-]{27,}\b", "[redacted-id]", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(
            sanitized,
            @"\b(user_id|job_id|name)\b\s*[:=]?\s*[^,; ]+",
            "$1 [redacted]",
            RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"(?<!\d)\+?\d{7,}(?!\d)", "[redacted-number]");
        sanitized = Regex.Replace(sanitized, @"\b[A-Za-z0-9_-]{24,}\b", "[redacted-token]");
        sanitized = Regex.Replace(sanitized, @"[\r\n\t]+", " ");
        sanitized = Regex.Replace(sanitized, @"\s{2,}", " ").Trim();
        return string.IsNullOrWhiteSpace(sanitized)
            ? "Provider rejected the request."
            : sanitized[..Math.Min(sanitized.Length, 200)];
    }

    private static string? SanitizeField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var sanitized = Regex.Replace(value, "[^A-Za-z0-9_.-]", string.Empty);
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized[..Math.Min(sanitized.Length, 64)];
    }

    private static string? InferRejectedField(string message)
    {
        var normalized = message.ToLowerInvariant();
        if (normalized.Contains("id_type") ||
            normalized.Contains("verification method") ||
            normalized.Contains("not enabled on your account"))
        {
            return "id_types";
        }
        if (normalized.Contains("callback")) return "callback_url";
        if (normalized.Contains("redirect")) return "redirect_url";
        if (normalized.Contains("privacy")) return "data_privacy_policy_url";
        if (normalized.Contains("partner")) return "partner_params";
        return null;
    }

    private SmileIdAuthentication CreateAuthentication()
    {
        var timestamp = _timeProvider.GetUtcNow().UtcDateTime.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture);
        return new SmileIdAuthentication(timestamp, GenerateSignature(timestamp));
    }

    public async Task<SmileIdJobStatusResponse> GetJobStatusAsync(
        string userId,
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var authentication = CreateAuthentication();
        using var response = await httpClient.PostAsJsonAsync(
            "v1/job_status",
            new
            {
                timestamp = authentication.Timestamp,
                signature = authentication.Signature,
                user_id = userId,
                job_id = jobId,
                partner_id = _settings.PartnerId,
                image_links = false,
                history = true
            },
            JsonOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ServiceUnavailableException(
                $"Smile ID job status request failed with HTTP {(int)response.StatusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<SmileIdJobStatusResponse>(
            JsonOptions,
            cancellationToken)
            ?? throw new ServiceUnavailableException("Smile ID returned an empty job status response.");

        if (string.IsNullOrWhiteSpace(result.Timestamp) ||
            string.IsNullOrWhiteSpace(result.Signature) ||
            !ValidateSignature(result.Timestamp, result.Signature))
        {
            throw new UnauthorizedException("Invalid Smile ID job status signature.");
        }

        return result;
    }

    public bool ValidateSignature(string timestamp, string signature)
    {
        byte[] supplied;
        try
        {
            supplied = Convert.FromBase64String(signature.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = Convert.FromBase64String(GenerateSignature(timestamp));
        return supplied.Length == expected.Length &&
               CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    private string GenerateSignature(string timestamp)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.ApiKey));
        var message = Encoding.UTF8.GetBytes(timestamp + _settings.PartnerId + "sid_request");
        return Convert.ToBase64String(hmac.ComputeHash(message));
    }
}
