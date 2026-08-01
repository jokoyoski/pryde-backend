using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Services.Providers.Dojah;
using Pryde.Services.Settings;

namespace Pryde.Tests.Services;

public class DojahApiClientTests
{
    [Fact]
    public void InterfaceAndImplementationReturnTypesMatch()
    {
        var interfaceMethod = typeof(IDojahApiClient).GetMethod(
            nameof(IDojahApiClient.GetVerificationAsync));
        var implementationMethod = typeof(DojahApiClient).GetMethod(
            nameof(DojahApiClient.GetVerificationAsync));

        Assert.NotNull(interfaceMethod);
        Assert.NotNull(implementationMethod);
        Assert.Equal(
            interfaceMethod.ReturnType,
            implementationMethod.ReturnType);
        Assert.Equal(
            typeof(Task<DojahVerificationDetailsResponseDto>),
            implementationMethod.ReturnType);
    }

    [Fact]
    public async Task GetVerificationUsesDocumentedEndpointHeadersAndMapsSanitizedDetails()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(WrappedCompletedVerificationJson, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var result = await client.GetVerificationAsync("verification-reference");

        Assert.Equal(
            "https://api.dojah.io/api/v1/kyc/verification?reference_id=verification-reference",
            handler.RequestUri?.AbsoluteUri);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("app-id", handler.AppId);
        Assert.Equal("private-key", handler.Authorization);
        Assert.False(handler.Authorization?.StartsWith(
            "Bearer ",
            StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, handler.AppIdHeaderCount);
        Assert.Equal(1, handler.AuthorizationHeaderCount);
        Assert.Equal("verification-reference", result.Reference);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("self", result.VerificationMode);
        Assert.Equal("kyc", result.VerificationType);
        Assert.True(result.IdResult);
        Assert.True(result.LivenessResult);
        Assert.True(result.GovernmentDataResult);
        Assert.Equal("DRIVERS_LICENSE", result.DocumentType);
        Assert.Equal("Ada Chiamaka Lovelace", result.FullName);
        Assert.Equal("Ada", result.FirstName);
        Assert.Equal("Lovelace", result.LastName);
        Assert.Equal("1990-01-02", result.DateOfBirth);
        Assert.Equal("Female", result.Gender);
        Assert.Equal("NG", result.Country);
        Assert.Equal("2020-03-04", result.IssueDate);
        Assert.Equal("2030-03-04", result.ExpiryDate);
        Assert.Equal("******7890", result.MaskedDocumentNumber);
        Assert.Equal("https://media.dojah.io/front.jpg", result.FrontDocumentImageUrl);
        Assert.Equal("https://media.dojah.io/back.jpg", result.BackDocumentImageUrl);
        Assert.Equal("https://media.dojah.io/selfie.jpg", result.SelfieImageUrl);
    }

    [Fact]
    public async Task UnwrappedProviderResponseRemainsSupported()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    CompletedVerificationJson,
                    Encoding.UTF8,
                    "application/json")
            });
        var client = CreateClient(handler);

        var result = await client.GetVerificationAsync(
            "verification-reference");

        Assert.Equal("verification-reference", result.Reference);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("Ada Chiamaka Lovelace", result.FullName);
        Assert.Equal("******7890", result.MaskedDocumentNumber);
    }

    [Fact]
    public async Task GetVerificationMasksShortDocumentNumberAndRejectsUnsafeImageUrls()
    {
        const string json = """
        {
          "reference_id": "short-reference",
          "verification_status": "Completed",
          "id_url": "http://media.dojah.io/front.jpg",
          "back_url": "/relative/back.jpg",
          "data": {
            "id": {
              "status": true,
              "data": {
                "id_data": {
                  "document_number": "1234"
                }
              }
            }
          }
        }
        """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var result = await client.GetVerificationAsync("short-reference");

        Assert.Equal("****", result.MaskedDocumentNumber);
        Assert.Null(result.FrontDocumentImageUrl);
        Assert.Null(result.BackDocumentImageUrl);
    }

    [Fact]
    public async Task SanitizedResultNeverSerializesProviderSecretsOrRawPayload()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(WrappedCompletedVerificationJson, Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var result = await client.GetVerificationAsync("verification-reference");
        var serialized = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("app-id", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-key", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("public-key", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1234567890", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("22110099887", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("ipinfo", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("device", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WrappedResponseMissingReferenceIsMappedToServiceUnavailable()
    {
        const string json = """
        {
          "entity": {
            "verification_status": "Completed"
          }
        }
        """;
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
            });
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            client.GetVerificationAsync("verification-reference"));
    }

    [Fact]
    public async Task NullEntityIsMappedToServiceUnavailable()
    {
        const string json = """
        {
          "entity": null
        }
        """;
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
            });
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            client.GetVerificationAsync("verification-reference"));
    }

    [Fact]
    public async Task MalformedJsonIsMappedToServiceUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{ "entity": """,
                    Encoding.UTF8,
                    "application/json")
            });
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            client.GetVerificationAsync("verification-reference"));
    }

    [Fact]
    public async Task TimeoutIsMappedToServiceUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("Timed out."));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            client.GetVerificationAsync("verification-reference"));
    }

    [Fact]
    public async Task ProviderUnavailableResponseIsMappedToServiceUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            client.GetVerificationAsync("verification-reference"));
    }

    [Fact]
    public async Task UnauthorizedIsMappedToServiceUnavailableWithoutLoggingSecrets()
    {
        const string appId = "1234-app-identifier-9999";
        const string privateKey = "test-private-secret-key-ABCD";
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(
                    $"{appId} {privateKey}",
                    Encoding.UTF8,
                    "application/json")
            });
        var logger = new TestLogger<DojahApiClient>();
        var settings = Settings();
        settings.AppId = appId;
        settings.PrivateKey = privateKey;
        var client = CreateClient(handler, settings, logger);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            client.GetVerificationAsync("verification-reference"));

        var logs = string.Join(Environment.NewLine, logger.Messages);
        Assert.Contains("Status: 401", logs);
        Assert.DoesNotContain(appId, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(privateKey, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"{appId} {privateKey}",
            logs,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestTrimsCredentialHeaders()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    CompletedVerificationJson,
                    Encoding.UTF8,
                    "application/json")
            });
        var settings = Settings();
        settings.AppId = " app-id ";
        settings.PrivateKey = " private-key ";
        var client = CreateClient(handler, settings);

        await client.GetVerificationAsync("verification reference");

        Assert.Equal("app-id", handler.AppId);
        Assert.Equal("private-key", handler.Authorization);
        Assert.Contains(
            "reference_id=verification%20reference",
            handler.RequestUri?.Query);
    }

    [Fact]
    public async Task ProviderNotFoundResponseIsMappedToNotFound()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            client.GetVerificationAsync("missing-reference"));
    }

    private static DojahApiClient CreateClient(
        HttpMessageHandler handler,
        DojahSettings? settings = null,
        ILogger<DojahApiClient>? logger = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.dojah.io/")
        };
        return new DojahApiClient(
            httpClient,
            Options.Create(settings ?? Settings()),
            logger ?? new TestLogger<DojahApiClient>());
    }

    private static DojahSettings Settings()
    {
        return new DojahSettings
        {
            Enabled = true,
            BaseUrl = "https://api.dojah.io",
            AppId = "app-id",
            ApiToken = "api-token",
            PublicKey = "public-key",
            PrivateKey = "private-key",
            ShareableLink =
                "https://identity.dojah.io/?widget_id=widget-test"
        };
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? AppId { get; private set; }
        public string? Authorization { get; private set; }
        public int AppIdHeaderCount { get; private set; }
        public int AuthorizationHeaderCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            AppIdHeaderCount = request.Headers.GetValues("AppId").Count();
            AuthorizationHeaderCount = request.Headers.GetValues("Authorization").Count();
            AppId = request.Headers.GetValues("AppId").Single();
            Authorization = request.Headers.GetValues("Authorization").Single();
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private const string CompletedVerificationJson = """
    {
      "reference_id": "verification-reference",
      "verification_status": "Completed",
      "verification_mode": "self",
      "verification_type": "kyc",
      "id_type": "DRIVERS_LICENSE",
      "id_url": "https://media.dojah.io/front.jpg",
      "back_url": "https://media.dojah.io/back.jpg",
      "metadata": {
        "ipinfo": "should-not-be-returned",
        "device": "should-not-be-returned"
      },
      "data": {
        "id": {
          "status": true,
          "data": {
            "id_url": "https://media.dojah.io/front.jpg",
            "back_url": "https://media.dojah.io/back.jpg",
            "id_data": {
              "first_name": "Ada",
              "middle_name": "Chiamaka",
              "last_name": "Lovelace",
              "nationality": "Nigerian",
              "date_issued": "2020-03-04",
              "expiry_date": "2030-03-04",
              "date_of_birth": "1990-01-02",
              "document_type": "DRIVERS_LICENSE",
              "document_number": "1234567890"
            }
          }
        },
        "selfie": {
          "status": true,
          "data": {
            "selfie_url": "https://media.dojah.io/selfie.jpg"
          }
        },
        "government_data": {
          "status": true,
          "data": {
            "bvn": {
              "entity": {
                "bvn": "22110099887",
                "gender": "Female"
              }
            }
          }
        },
        "countries": {
          "data": {
            "country": "NG"
          }
        }
      }
    }
    """;

    private static readonly string WrappedCompletedVerificationJson = $$"""
    {
      "entity": {{CompletedVerificationJson}}
    }
    """;
}
