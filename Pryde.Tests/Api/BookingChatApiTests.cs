using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Pryde.Api.Controllers.V1;
using Pryde.Api.Hubs;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Constants;

namespace Pryde.Tests.Api;

public class BookingChatApiTests
{
    [Fact]
    public void ChatHubRequiresAuthentication()
    {
        Assert.NotNull(typeof(ChatHub).GetCustomAttributes(
                typeof(AuthorizeAttribute),
                true)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault());
    }

    [Fact]
    public void ParticipantChatControllerAllowsOnlyDriverAndPassengerRoles()
    {
        var authorize = Assert.Single(typeof(BookingChatsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());

        Assert.Equal(
            RoleNames.Driver + "," + RoleNames.Passenger,
            authorize.Roles);
    }

    [Fact]
    public void AdminChatControllerIsAdminRestrictedAndReadOnly()
    {
        var authorize = Assert.Single(typeof(AdminBookingChatsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());
        var publicActions = typeof(AdminBookingChatsController)
            .GetMethods()
            .Where(method => method.DeclaringType ==
                typeof(AdminBookingChatsController))
            .ToList();

        Assert.Equal(RoleNames.AdminOrSuperAdmin, authorize.Roles);
        Assert.All(publicActions, action =>
            Assert.NotNull(action.GetCustomAttributes(
                    typeof(HttpGetAttribute),
                    true)
                .SingleOrDefault()));
        Assert.DoesNotContain(publicActions, action =>
            action.GetCustomAttributes(typeof(HttpPostAttribute), true).Any() ||
            action.GetCustomAttributes(typeof(HttpPatchAttribute), true).Any() ||
            action.GetCustomAttributes(typeof(HttpDeleteAttribute), true).Any());
    }

    [Fact]
    public async Task RealtimeSenderTargetsOnlyDriverAndPassenger()
    {
        var driverId = Guid.NewGuid();
        var passengerId = Guid.NewGuid();
        var client = new RecordingChatClient();
        var clients = new RecordingChatHubClients(client);
        var sender = new SignalRChatRealtimeSender(
            new TestChatHubContext(clients));
        var message = new ChatMessageResponseDto
        {
            MessageId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            SenderId = passengerId,
            MessageText = "Hello",
            SentAt = DateTime.UtcNow
        };

        await sender.SendAsync(driverId, passengerId, message);

        Assert.Equal(
            [driverId.ToString(), passengerId.ToString()],
            clients.UserIds);
        Assert.Same(message, client.Message);
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public ChatMessageResponseDto? Message { get; private set; }

        public Task ReceiveChatMessage(ChatMessageResponseDto message)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingChatHubClients(IChatClient client)
        : IHubClients<IChatClient>
    {
        public IReadOnlyList<string> UserIds { get; private set; } = [];
        public IChatClient All => throw new NotSupportedException();
        public IChatClient AllExcept(
            IReadOnlyList<string> excludedConnectionIds) =>
            throw new NotSupportedException();
        public IChatClient Client(string connectionId) =>
            throw new NotSupportedException();
        public IChatClient Clients(IReadOnlyList<string> connectionIds) =>
            throw new NotSupportedException();
        public IChatClient Group(string groupName) =>
            throw new NotSupportedException();
        public IChatClient GroupExcept(
            string groupName,
            IReadOnlyList<string> excludedConnectionIds) =>
            throw new NotSupportedException();
        public IChatClient Groups(IReadOnlyList<string> groupNames) =>
            throw new NotSupportedException();
        public IChatClient User(string userId) =>
            throw new NotSupportedException();

        public IChatClient Users(IReadOnlyList<string> userIds)
        {
            UserIds = userIds;
            return client;
        }
    }

    private sealed class TestChatHubContext(
        IHubClients<IChatClient> clients)
        : IHubContext<ChatHub, IChatClient>
    {
        public IHubClients<IChatClient> Clients { get; } = clients;
        public IGroupManager Groups { get; } = new TestGroupManager();
    }

    private sealed class TestGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
