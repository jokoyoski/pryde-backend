using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pryde.Services.Providers.Paystack;
using Pryde.Services.Settings;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class PaystackClientTests
{
    [Fact]
    public async Task GetBanksUsesExpectedRequestAndReturnsOnlyActiveBanks()
    {
        const string responseJson =
            "{\"status\":true,\"message\":\"Banks retrieved\"," +
            "\"data\":[{\"name\":\"Active Bank\",\"code\":\"058\"," +
            "\"active\":true},{\"name\":\"Inactive Bank\"," +
            "\"code\":\"999\",\"active\":false}]}";

        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            responseJson);

        using (var httpClient = new HttpClient(handler))
        {
            var client = CreateClient(httpClient);

            var banks = await client.GetBanksAsync();

            var bank = Assert.Single(banks);
            Assert.Equal("Active Bank", bank.Name);
            Assert.Equal("058", bank.Code);
            Assert.Equal(
                "/bank?country=nigeria&currency=NGN",
                handler.RequestPathAndQuery);
            Assert.Equal("Bearer", handler.AuthorizationScheme);
            Assert.Equal(
                "test-secret",
                handler.AuthorizationParameter);
            Assert.Contains(
                "application/json",
                handler.AcceptHeaders);
        }
    }

    [Fact]
    public async Task ResolveAccountUsesExpectedRequestAndParsesResponse()
    {
        const string responseJson =
            "{\"status\":true,\"message\":\"Account number resolved\"," +
            "\"data\":{\"account_number\":\"0123456789\"," +
            "\"account_name\":\"Example Account Name\"}}";

        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            responseJson);

        using (var httpClient = new HttpClient(handler))
        {
            var client = CreateClient(httpClient);

            var account = await client.ResolveAccountAsync(
                "058",
                "0123456789");

            Assert.Equal("0123456789", account.AccountNumber);
            Assert.Equal("Example Account Name", account.AccountName);
            Assert.Equal(
                "/bank/resolve?account_number=0123456789&bank_code=058",
                handler.RequestPathAndQuery);
        }
    }

    [Fact]
    public async Task CreateTransferRecipientUsesNubanPayloadAndParsesCode()
    {
        const string responseJson =
            "{\"status\":true," +
            "\"message\":\"Transfer recipient created successfully\"," +
            "\"data\":{\"recipient_code\":\"RCP_test_recipient\"}}";

        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            responseJson);

        using (var httpClient = new HttpClient(handler))
        {
            var client = CreateClient(httpClient);

            var recipient = await client.CreateTransferRecipientAsync(
                "058",
                "0123456789",
                "Example Account Name");

            Assert.Equal(
                "RCP_test_recipient",
                recipient.RecipientCode);
            Assert.Equal(HttpMethod.Post, handler.RequestMethod);
            Assert.Equal(
                "/transferrecipient",
                handler.RequestPathAndQuery);
            Assert.Contains(
                "\"type\":\"nuban\"",
                handler.RequestBody);
            Assert.Contains(
                "\"name\":\"Example Account Name\"",
                handler.RequestBody);
            Assert.Contains(
                "\"account_number\":\"0123456789\"",
                handler.RequestBody);
            Assert.Contains(
                "\"bank_code\":\"058\"",
                handler.RequestBody);
            Assert.Contains(
                "\"currency\":\"NGN\"",
                handler.RequestBody);
        }
    }

    [Fact]
    public async Task CreateTransferUsesKoboRecipientReferenceAndNgn()
    {
        const string responseJson =
            "{\"status\":true,\"message\":\"Transfer queued\"," +
            "\"data\":{\"reference\":\"pryde-wd-test-reference\"," +
            "\"status\":\"success\",\"transfer_code\":\"TRF_test\"}}";

        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            responseJson);

        using (var httpClient = new HttpClient(handler))
        {
            var client = CreateClient(httpClient);

            var transfer = await client.CreateTransferAsync(
                "RCP_test_recipient",
                500000,
                "pryde-wd-test-reference",
                "Pryde driver withdrawal");

            Assert.Equal("success", transfer.Status);
            Assert.Equal(
                "pryde-wd-test-reference",
                transfer.Reference);
            Assert.Equal(HttpMethod.Post, handler.RequestMethod);
            Assert.Equal("/transfer", handler.RequestPathAndQuery);
            Assert.Contains("\"source\":\"balance\"", handler.RequestBody);
            Assert.Contains("\"amount\":500000", handler.RequestBody);
            Assert.Contains(
                "\"recipient\":\"RCP_test_recipient\"",
                handler.RequestBody);
            Assert.Contains(
                "\"reference\":\"pryde-wd-test-reference\"",
                handler.RequestBody);
            Assert.Contains("\"currency\":\"NGN\"", handler.RequestBody);
        }
    }

    [Fact]
    public async Task DevelopmentTransferBypassesHttpWhenEnabled()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.InternalServerError,
            "{}");

        using (var httpClient = new HttpClient(handler))
        {
            var client = CreateClient(
                httpClient,
                "Development",
                true);

            var transfer = await client.CreateTransferAsync(
                "RCP_test_recipient",
                500000,
                "pryde-wd-development-reference",
                "Pryde driver withdrawal");

            Assert.Equal("success", transfer.Status);
            Assert.Equal(
                "pryde-wd-development-reference",
                transfer.Reference);
            Assert.StartsWith("DEV-", transfer.TransferCode);
            Assert.Null(handler.RequestMethod);
        }
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", true)]
    public async Task TransferUsesPaystackUnlessBothBypassConditionsMatch(
        string environmentName,
        bool useDevelopmentTransfer)
    {
        const string responseJson =
            "{\"status\":true,\"message\":\"Transfer queued\"," +
            "\"data\":{\"reference\":\"pryde-wd-real-reference\"," +
            "\"status\":\"success\",\"transfer_code\":\"TRF_real\"}}";
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            responseJson);

        using (var httpClient = new HttpClient(handler))
        {
            var client = CreateClient(
                httpClient,
                environmentName,
                useDevelopmentTransfer);

            var transfer = await client.CreateTransferAsync(
                "RCP_test_recipient",
                500000,
                "pryde-wd-real-reference",
                "Pryde driver withdrawal");

            Assert.Equal("TRF_real", transfer.TransferCode);
            Assert.Equal(HttpMethod.Post, handler.RequestMethod);
            Assert.Equal("/transfer", handler.RequestPathAndQuery);
        }
    }

    [Fact]
    public void PaystackSettingsValidateOnlyWhenEnabled()
    {
        var validator = new PaystackSettingsValidator();

        var disabledResult = validator.Validate(
            null,
            new PaystackSettings
            {
                Enabled = false,
                SecretKey = string.Empty
            });

        var enabledResult = validator.Validate(
            null,
            new PaystackSettings
            {
                Enabled = true,
                SecretKey = string.Empty
            });

        Assert.True(disabledResult.Succeeded);
        Assert.True(enabledResult.Failed);
        Assert.Contains(
            nameof(PaystackSettings.SecretKey),
            enabledResult.FailureMessage);
    }

    private static PaystackClient CreateClient(
        HttpClient httpClient,
        string environmentName = "Production",
        bool useDevelopmentTransfer = false)
    {
        httpClient.BaseAddress = new Uri(
            "https://api.paystack.co/");

        return new PaystackClient(
            httpClient,
            Options.Create(new PaystackSettings
            {
                Enabled = true,
                UseDevelopmentTransfer = useDevelopmentTransfer,
                BaseUrl = "https://api.paystack.co",
                SecretKey = "test-secret"
            }),
            NullLogger<PaystackClient>.Instance,
            new TestHostEnvironment
            {
                EnvironmentName = environmentName
            });
    }

    private sealed class RecordingHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseJson;

        public RecordingHttpMessageHandler(
            HttpStatusCode statusCode,
            string responseJson)
        {
            _statusCode = statusCode;
            _responseJson = responseJson;
        }

        public HttpMethod? RequestMethod { get; private set; }
        public string RequestPathAndQuery { get; private set; } =
            string.Empty;
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public IReadOnlyList<string> AcceptHeaders { get; private set; } =
            new List<string>();
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestMethod = request.Method;
            RequestPathAndQuery =
                request.RequestUri?.PathAndQuery ?? string.Empty;
            AuthorizationScheme =
                request.Headers.Authorization?.Scheme;
            AuthorizationParameter =
                request.Headers.Authorization?.Parameter;
            AcceptHeaders = request.Headers.Accept
                .Select(header => header.MediaType ?? string.Empty)
                .ToList();

            if (request.Content != null)
            {
                RequestBody = await request.Content.ReadAsStringAsync(
                    cancellationToken);
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(
                    _responseJson,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
