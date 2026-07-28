using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pryde.Domain.Common.Exceptions;
using Pryde.Services.Settings;

namespace Pryde.Services.Providers.Paystack;

public class PaystackClient : IPaystackClient
{
    private readonly HttpClient _httpClient;
    private readonly PaystackSettings _settings;
    private readonly ILogger<PaystackClient> _logger;

    public PaystackClient(
        HttpClient httpClient,
        IOptions<PaystackSettings> options,
        ILogger<PaystackClient> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PaystackBank>> GetBanksAsync(
        CancellationToken cancellationToken = default)
    {
        using (var request = new HttpRequestMessage(
                   HttpMethod.Get,
                   "bank?country=nigeria&currency=NGN"))
        {
            var banks = await SendAsync<List<PaystackBank>>(
                request,
                cancellationToken);

            return banks
                .Where(bank => bank.Active != false)
                .OrderBy(bank => bank.Name)
                .ToList();
        }
    }

    public async Task<PaystackResolvedAccount> ResolveAccountAsync(
        string bankCode,
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        var encodedBankCode = Uri.EscapeDataString(bankCode);
        var encodedAccountNumber = Uri.EscapeDataString(accountNumber);
        var requestUrl =
            $"bank/resolve?account_number={encodedAccountNumber}&bank_code={encodedBankCode}";

        using (var request = new HttpRequestMessage(
                   HttpMethod.Get,
                   requestUrl))
        {
            return await SendAsync<PaystackResolvedAccount>(
                request,
                cancellationToken);
        }
    }

    public async Task<PaystackTransferRecipient> CreateTransferRecipientAsync(
        string bankCode,
        string accountNumber,
        string accountName,
        CancellationToken cancellationToken = default)
    {
        var providerRequest = new PaystackTransferRecipientRequest
        {
            Name = accountName,
            AccountNumber = accountNumber,
            BankCode = bankCode
        };

        using (var request = new HttpRequestMessage(
                   HttpMethod.Post,
                   "transferrecipient"))
        {
            request.Content = JsonContent.Create(providerRequest);

            return await SendAsync<PaystackTransferRecipient>(
                request,
                cancellationToken);
        }
    }

    private async Task<T> SendAsync<T>(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
    {
        EnsureAvailable();

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _settings.SecretKey);

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(
                request,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Paystack request failed before a response was received.");

            throw new ServiceUnavailableException(
                "Paystack is currently unavailable.");
        }

        using (response)
        {
            PaystackResponse<T>? providerResponse;

            try
            {
                providerResponse = await response.Content
                    .ReadFromJsonAsync<PaystackResponse<T>>(
                        cancellationToken: cancellationToken);
            }
            catch (JsonException exception)
            {
                _logger.LogError(
                    exception,
                    "Paystack returned an invalid response.");

                throw new ServiceUnavailableException(
                    "Paystack returned an invalid response.");
            }

            if (!response.IsSuccessStatusCode ||
                providerResponse == null ||
                !providerResponse.Status ||
                providerResponse.Data == null)
            {
                _logger.LogWarning(
                    "Paystack rejected a request with status code {StatusCode}.",
                    (int)response.StatusCode);

                throw new ServiceUnavailableException(
                    "Paystack could not complete the request.");
            }

            return providerResponse.Data;
        }
    }

    private void EnsureAvailable()
    {
        if (!_settings.Enabled)
        {
            throw new ServiceUnavailableException(
                "Paystack is currently unavailable.");
        }

        if (string.IsNullOrWhiteSpace(_settings.SecretKey) ||
            !Uri.TryCreate(
                _settings.BaseUrl,
                UriKind.Absolute,
                out var baseUrl) ||
            baseUrl.Scheme != Uri.UriSchemeHttps)
        {
            throw new ServiceUnavailableException(
                "Paystack is not configured.");
        }
    }
}
