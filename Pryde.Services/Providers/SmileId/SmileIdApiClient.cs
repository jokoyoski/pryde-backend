using System.Net.Http.Json;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pryde.Domain.Common.Exceptions;
using Pryde.Services.Settings;

namespace Pryde.Services.Providers.SmileId;

public sealed class SmileIdApiClient(
    HttpClient httpClient,
    IOptions<SmileIdSettings> options,
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
            throw new ServiceUnavailableException(
                $"Smile ID link creation failed with HTTP {(int)response.StatusCode}.");
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
