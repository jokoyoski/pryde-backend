using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Services.Settings;

namespace Pryde.Services.Providers.Dojah;

public sealed class DojahApiClient(
    HttpClient httpClient,
    IOptions<DojahSettings> options,
    ILogger<DojahApiClient> logger) : IDojahApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<DojahVerificationDetailsResponseDto> GetVerificationAsync(
        string dojahReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dojahReference))
        {
            throw new ValidationException("Dojah reference is required.");
        }

        var settings = options.Value;
        EnsureConfigured(settings);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v1/kyc/verification?reference_id={Uri.EscapeDataString(dojahReference.Trim())}");
        request.Headers.TryAddWithoutValidation("AppId", settings.AppId.Trim());
        request.Headers.TryAddWithoutValidation("Authorization", settings.PrivateKey.Trim());

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ServiceUnavailableException("Dojah verification details are temporarily unavailable.");
        }
        catch (HttpRequestException)
        {
            throw new ServiceUnavailableException("Dojah verification details are temporarily unavailable.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogWarning(
                    "Dojah verification details were not found. Status: {StatusCode}.",
                    (int)response.StatusCode);

                throw new NotFoundException("Dojah verification", dojahReference);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Dojah verification details request failed. Status: {StatusCode}. Reason: {ReasonPhrase}.",
                    (int)response.StatusCode,
                    response.ReasonPhrase);

                throw new ServiceUnavailableException("Dojah verification details are temporarily unavailable.");
            }

            DojahVerificationEnvelope? envelope;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<DojahVerificationEnvelope>(
                    SerializerOptions,
                    cancellationToken);
            }
            catch (JsonException)
            {
                throw new ServiceUnavailableException("Dojah returned an invalid verification response.");
            }

            var providerResponse = envelope?.GetVerification();
            if (providerResponse is null || string.IsNullOrWhiteSpace(providerResponse.ReferenceId))
            {
                throw new ServiceUnavailableException("Dojah returned an invalid verification response.");
            }

            return Map(providerResponse);
        }
    }

    private static DojahVerificationDetailsResponseDto Map(DojahVerificationApiResponse response)
    {
        var idData = response.Data?.Id?.Data?.IdData;
        var userData = response.Data?.UserData?.Data;
        var fullName = BuildFullName(
            idData?.FirstName ?? userData?.FirstName,
            idData?.MiddleName,
            idData?.LastName ?? userData?.LastName);

        return new DojahVerificationDetailsResponseDto
        {
            Reference = response.ReferenceId!,
            Status = response.VerificationStatus,
            VerificationMode = response.VerificationMode,
            VerificationType = response.VerificationType,
            IdResult = response.Data?.Id?.Status,
            LivenessResult = response.Data?.Selfie?.Status,
            GovernmentDataResult = response.Data?.GovernmentData?.Status,
            DocumentType = idData?.DocumentType ?? response.IdType,
            FullName = fullName,
            DateOfBirth = idData?.DateOfBirth ?? userData?.DateOfBirth,
            Country = response.Data?.Countries?.Data?.Country,
            IssueDate = idData?.DateIssued,
            ExpiryDate = idData?.ExpiryDate,
            MaskedDocumentNumber = MaskDocumentNumber(idData?.DocumentNumber),
            FrontDocumentImageUrl = GetSafeHttpsUrl(response.Data?.Id?.Data?.IdUrl ?? response.IdUrl),
            BackDocumentImageUrl = GetSafeHttpsUrl(response.Data?.Id?.Data?.BackUrl ?? response.BackUrl)
        };
    }

    private static string? BuildFullName(string? firstName, string? middleName, string? lastName)
    {
        var parts = new[] { firstName, middleName, lastName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim());
        var fullName = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(fullName) ? null : fullName;
    }

    private static string? MaskDocumentNumber(string? documentNumber)
    {
        if (string.IsNullOrWhiteSpace(documentNumber))
        {
            return null;
        }

        var value = documentNumber.Trim();
        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        return $"{new string('*', value.Length - 4)}{value[^4..]}";
    }

    private static string? GetSafeHttpsUrl(string? value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsoluteUri;
        }

        return null;
    }

    private void EnsureConfigured(DojahSettings settings)
    {
        if (!settings.Enabled ||
            string.IsNullOrWhiteSpace(settings.AppId) ||
            string.IsNullOrWhiteSpace(settings.PrivateKey) ||
            httpClient.BaseAddress is null)
        {
            throw new ServiceUnavailableException("Dojah verification details are not configured.");
        }
    }
}
