using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pryde.Api.Extension;
using Pryde.Api.Hubs;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Enums;

namespace Pryde.Tests.Api;

public class NotificationHubTests
{
    [Fact]
    public async Task HubConnectionRequiresAuthentication()
    {
        var authorizeData = typeof(NotificationHub)
            .GetCustomAttributes(
                typeof(AuthorizeAttribute),
                true)
            .Cast<IAuthorizeData>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        using var provider = services.BuildServiceProvider();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<
                IAuthorizationPolicyProvider>(),
            authorizeData);
        var authorizationService = provider
            .GetRequiredService<IAuthorizationService>();

        var unauthenticated = await authorizationService
            .AuthorizeAsync(
                new ClaimsPrincipal(
                    new ClaimsIdentity()),
                null,
                policy!);
        var authenticated = await authorizationService
            .AuthorizeAsync(
                new ClaimsPrincipal(
                    new ClaimsIdentity(
                        [
                            new Claim(
                                ClaimTypes.NameIdentifier,
                                Guid.NewGuid().ToString())
                        ],
                        "Test")),
                null,
                policy!);

        Assert.False(unauthenticated.Succeeded);
        Assert.True(authenticated.Succeeded);
    }

    [Fact]
    public async Task JwtQueryTokenIsReadOnlyForNotificationHub()
    {
        using var provider = CreateAuthenticationServices();
        var options = provider
            .GetRequiredService<
                IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        var hubContext = CreateMessageReceivedContext(
            options,
            "/hubs/notifications",
            "hub-token");
        await options.Events.OnMessageReceived(hubContext);

        var apiContext = CreateMessageReceivedContext(
            options,
            "/api/v1/notifications",
            "api-query-token");
        await options.Events.OnMessageReceived(apiContext);

        Assert.Equal("hub-token", hubContext.Token);
        Assert.Null(apiContext.Token);
    }

    [Fact]
    public void DefaultUserIdProviderUsesNameIdentifierClaim()
    {
        var userId = Guid.NewGuid();
        var connection = new DefaultConnectionContext();
        connection.Features.Set<IConnectionUserFeature>(
            new TestConnectionUserFeature
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        [
                            new Claim(
                                ClaimTypes.NameIdentifier,
                                userId.ToString())
                        ],
                        "Test"))
            });
        var hubConnection = new HubConnectionContext(
            connection,
            new HubConnectionContextOptions(),
            NullLoggerFactory.Instance);

        var result = new DefaultUserIdProvider()
            .GetUserId(hubConnection);

        Assert.Equal(userId.ToString(), result);
    }

    [Fact]
    public async Task RealtimeSenderDeliversOnlyToIntendedUser()
    {
        var intendedUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var intendedClient = new RecordingNotificationClient();
        var otherClient = new RecordingNotificationClient();
        var clients = new RecordingHubClients(
            new Dictionary<string, INotificationClient>
            {
                [intendedUserId.ToString()] = intendedClient,
                [otherUserId.ToString()] = otherClient
            });
        var sender = new SignalRNotificationRealtimeSender(
            new TestHubContext(clients));
        var notification = new NotificationResponseDto
        {
            Id = Guid.NewGuid(),
            Type = NotificationType.BookingApproved,
            Title = "Booking approved",
            Message = "Your booking was approved.",
            CreatedAt = DateTime.UtcNow
        };

        await sender.SendAsync(
            intendedUserId,
            notification);

        Assert.Equal(
            intendedUserId.ToString(),
            clients.LastRequestedUserId);
        Assert.Same(
            notification,
            Assert.Single(intendedClient.Notifications));
        Assert.Empty(otherClient.Notifications);
    }

    private static ServiceProvider CreateAuthenticationServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["JwtSettings:Key"] =
                        "test-signing-key-that-is-long-enough-for-hmac",
                    ["JwtSettings:Issuer"] = "test-issuer",
                    ["JwtSettings:Audience"] = "test-audience"
                })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthenticationConfiguration(configuration);
        return services.BuildServiceProvider();
    }

    private static MessageReceivedContext CreateMessageReceivedContext(
        JwtBearerOptions options,
        string path,
        string accessToken)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.QueryString =
            new QueryString($"?access_token={accessToken}");
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));
        return new MessageReceivedContext(
            httpContext,
            scheme,
            options);
    }

    private sealed class TestConnectionUserFeature
        : IConnectionUserFeature
    {
        public ClaimsPrincipal? User { get; set; }
    }

    private sealed class RecordingNotificationClient
        : INotificationClient
    {
        public List<NotificationResponseDto> Notifications { get; } = [];

        public Task ReceiveNotification(
            NotificationResponseDto notification)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHubClients(
        IReadOnlyDictionary<string, INotificationClient> users)
        : IHubClients<INotificationClient>
    {
        public string? LastRequestedUserId { get; private set; }

        public INotificationClient All =>
            throw new NotSupportedException();

        public INotificationClient AllExcept(
            IReadOnlyList<string> excludedConnectionIds)
        {
            throw new NotSupportedException();
        }

        public INotificationClient Client(string connectionId)
        {
            throw new NotSupportedException();
        }

        public INotificationClient Clients(
            IReadOnlyList<string> connectionIds)
        {
            throw new NotSupportedException();
        }

        public INotificationClient Group(string groupName)
        {
            throw new NotSupportedException();
        }

        public INotificationClient Groups(
            IReadOnlyList<string> groupNames)
        {
            throw new NotSupportedException();
        }

        public INotificationClient GroupExcept(
            string groupName,
            IReadOnlyList<string> excludedConnectionIds)
        {
            throw new NotSupportedException();
        }

        public INotificationClient User(string userId)
        {
            LastRequestedUserId = userId;
            return users[userId];
        }

        public INotificationClient Users(
            IReadOnlyList<string> userIds)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestHubContext(
        IHubClients<INotificationClient> clients)
        : IHubContext<NotificationHub, INotificationClient>
    {
        public IHubClients<INotificationClient> Clients { get; } =
            clients;

        public IGroupManager Groups { get; } =
            new TestGroupManager();
    }

    private sealed class TestGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
