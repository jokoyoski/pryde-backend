using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Service.Interface;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class BookingChatServiceTests
{
    [Fact]
    public async Task ApprovedBookingAllowsDriverAndPassengerAccess()
    {
        var context = CreateContext();

        var passengerChat = await context.Service.GetAsync(
            context.Booking.Id,
            context.PassengerId);
        var driverChat = await context.Service.GetAsync(
            context.Booking.Id,
            context.DriverId);

        Assert.Equal(passengerChat.ChatId, driverChat.ChatId);
        Assert.Equal(context.Booking.Id, passengerChat.BookingId);
        Assert.Equal(BookingChatStatus.Open, passengerChat.Status);
        Assert.True(passengerChat.CanSend);
        Assert.Single(context.UnitOfWork.BookingChatRepository.Items);
    }

    [Theory]
    [InlineData(BookingStatus.Pending)]
    [InlineData(BookingStatus.Declined)]
    public async Task PendingAndDeclinedBookingsCannotAccessChat(
        BookingStatus status)
    {
        var context = CreateContext(status, approvedAt: null);

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.GetAsync(
                context.Booking.Id,
                context.PassengerId));
    }

    [Fact]
    public async Task UnrelatedUserCannotAccessChat()
    {
        var context = CreateContext();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            context.Service.GetAsync(
                context.Booking.Id,
                Guid.NewGuid()));
    }

    [Fact]
    public async Task SendingPersistsAndDeliversMessageToBothParticipants()
    {
        var context = CreateContext();
        var request = MessageRequest("Pickup point confirmed.");

        var response = await context.Service.SendAsync(
            context.Booking.Id,
            context.PassengerId,
            false,
            request);

        var stored = Assert.Single(
            context.UnitOfWork.BookingChatRepository.Messages);
        Assert.Equal(response.MessageId, stored.Id);
        Assert.Equal(request.ClientMessageId, response.ClientMessageId);
        Assert.Equal(context.PassengerId, response.SenderId);
        Assert.Equal("Pat Passenger", response.SenderName);
        Assert.Equal("Pickup point confirmed.", response.MessageText);
        Assert.Equal(context.DriverId, context.RealtimeSender.DriverId);
        Assert.Equal(context.PassengerId, context.RealtimeSender.PassengerId);
        Assert.Same(response, context.RealtimeSender.Message);
    }

    [Fact]
    public async Task SentMessageCanBeReadFromHistory()
    {
        var context = CreateContext();
        var sent = await context.Service.SendAsync(
            context.Booking.Id,
            context.DriverId,
            false,
            MessageRequest("I have arrived."));

        var history = await context.Service.GetMessagesAsync(
            context.Booking.Id,
            context.PassengerId,
            new BookingChatMessagesRequestDto());

        var received = Assert.Single(history.Items);
        Assert.Equal(sent.MessageId, received.MessageId);
        Assert.Equal("Ada Driver", received.SenderName);
        Assert.Equal("I have arrived.", received.MessageText);
    }

    [Fact]
    public async Task HistoryUsesStableNewestFirstPagination()
    {
        var context = CreateContext();
        var chat = await context.Service.GetAsync(
            context.Booking.Id,
            context.PassengerId);
        var start = DateTime.UtcNow.AddMinutes(-5);
        for (var index = 0; index < 5; index++)
        {
            context.UnitOfWork.BookingChatRepository.Messages.Add(
                new ChatMessage
                {
                    ChatId = chat.ChatId,
                    SenderId = context.PassengerId,
                    Sender = context.Booking.Passenger,
                    ClientMessageId = Guid.NewGuid(),
                    MessageText = $"Message {index}",
                    SentAt = start.AddMinutes(index)
                });
        }

        var page = await context.Service.GetMessagesAsync(
            context.Booking.Id,
            context.DriverId,
            new BookingChatMessagesRequestDto
            {
                PageNumber = 2,
                PageSize = 2
            });

        Assert.Equal(["Message 2", "Message 1"],
            page.Items.Select(message => message.MessageText));
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyOrWhitespaceMessageIsRejected(string messageText)
    {
        var context = CreateContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.SendAsync(
                context.Booking.Id,
                context.PassengerId,
                false,
                MessageRequest(messageText)));
    }

    [Fact]
    public async Task EmptyClientMessageIdIsRejected()
    {
        var context = CreateContext();
        var request = MessageRequest("Hello");
        request.ClientMessageId = Guid.Empty;

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.SendAsync(
                context.Booking.Id,
                context.PassengerId,
                false,
                request));
    }

    [Fact]
    public async Task OversizedMessageIsRejected()
    {
        var context = CreateContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.SendAsync(
                context.Booking.Id,
                context.PassengerId,
                false,
                MessageRequest(new string(
                    'x',
                    BookingChatService.MaximumMessageLength + 1))));
    }

    [Fact]
    public async Task DuplicateClientMessageIdReturnsOriginalMessageOnce()
    {
        var context = CreateContext();
        var request = MessageRequest("Only once");

        var first = await context.Service.SendAsync(
            context.Booking.Id,
            context.PassengerId,
            false,
            request);
        var second = await context.Service.SendAsync(
            context.Booking.Id,
            context.PassengerId,
            false,
            request);

        Assert.Equal(first.MessageId, second.MessageId);
        Assert.Single(context.UnitOfWork.BookingChatRepository.Messages);
        Assert.Equal(1, context.RealtimeSender.SendCount);
    }

    [Fact]
    public async Task DriverEndTripLocksSending()
    {
        var context = CreateContext();
        context.Booking.PaidAt = DateTime.UtcNow.AddMinutes(-10);
        context.Trip.Status = TripStatus.InProgress;

        await TestData.CreateTripService(context.UnitOfWork)
            .EndAsync(context.Trip.Id, context.DriverId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.SendAsync(
                context.Booking.Id,
                context.PassengerId,
                false,
                MessageRequest("Too late")));
        Assert.NotNull(context.Trip.DriverEndedAt);
    }

    [Fact]
    public async Task ClosedChatHistoryRemainsReadable()
    {
        var context = CreateContext();
        await context.Service.SendAsync(
            context.Booking.Id,
            context.PassengerId,
            false,
            MessageRequest("Before end"));
        context.Trip.DriverEndedAt = DateTime.UtcNow;
        context.Trip.Status = TripStatus.DropoffConfirmationPending;

        var chat = await context.Service.GetAsync(
            context.Booking.Id,
            context.DriverId);
        var history = await context.Service.GetMessagesAsync(
            context.Booking.Id,
            context.DriverId,
            new BookingChatMessagesRequestDto());

        Assert.Equal(BookingChatStatus.Closed, chat.Status);
        Assert.False(chat.CanSend);
        Assert.Single(history.Items);
    }

    [Fact]
    public async Task CompletedChatExposesThirtyDayRetentionDate()
    {
        var context = CreateContext();
        var completedAt = new DateTime(
            2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);
        context.Trip.DriverEndedAt = completedAt.AddMinutes(-5);
        context.Trip.CompletedAt = completedAt;
        context.Trip.Status = TripStatus.Completed;

        var chat = await context.Service.GetAsync(
            context.Booking.Id,
            context.PassengerId);

        Assert.Equal(completedAt.AddDays(30), chat.RetainUntil);
    }

    [Fact]
    public async Task AdminCanSearchAndReadChatButCannotSend()
    {
        var context = CreateContext();
        await context.Service.SendAsync(
            context.Booking.Id,
            context.PassengerId,
            false,
            MessageRequest("Support evidence"));

        var chats = await context.Service.AdminSearchAsync(
            new AdminBookingChatsRequestDto
            {
                BookingId = context.Booking.Id,
                TripId = context.Trip.Id
            });
        var history = await context.Service.AdminGetMessagesAsync(
            context.Booking.Id,
            new BookingChatMessagesRequestDto());
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            context.Service.SendAsync(
                context.Booking.Id,
                Guid.NewGuid(),
                true,
                MessageRequest("Admin write")));

        Assert.False(Assert.Single(chats.Items).CanSend);
        Assert.Single(history.Items);
    }

    [Fact]
    public async Task ApprovalCreatesExactlyOneBookingChat()
    {
        var context = CreateContext(
            BookingStatus.Pending,
            approvedAt: null);
        context.Trip.DepartureTime = DateTime.UtcNow.AddHours(2);

        await new TripBookingService(context.UnitOfWork)
            .ApproveAsync(context.Booking.Id, context.DriverId);

        var chat = Assert.Single(
            context.UnitOfWork.BookingChatRepository.Items);
        Assert.Equal(context.Booking.Id, chat.BookingId);
    }

    private static ChatTestContext CreateContext(
        BookingStatus status = BookingStatus.Approved,
        DateTime? approvedAt = null)
    {
        var (unitOfWork, driverId, vehicle) =
            TestData.CreateDriverContext();
        var trip = TestData.OpenTrip(driverId, vehicle);
        unitOfWork.TripRepository.Items.Add(trip);
        var passengerId = Guid.NewGuid();
        var booking = TestData.Booking(trip, passengerId, status);
        booking.ApprovedAt = approvedAt ??
            (status == BookingStatus.Approved
                ? DateTime.UtcNow.AddMinutes(-5)
                : null);
        unitOfWork.TripBookingRepository.Items.Add(booking);
        unitOfWork.UserRepository.Items.Add(trip.Driver);
        unitOfWork.UserRepository.Items.Add(booking.Passenger);
        unitOfWork.ProfileRepository.Items.Add(booking.Passenger.Profile!);
        var realtimeSender = new RecordingChatRealtimeSender();
        return new ChatTestContext(
            unitOfWork,
            new BookingChatService(
                unitOfWork,
                realtimeSender,
                Microsoft.Extensions.Logging.Abstractions
                    .NullLogger<BookingChatService>.Instance),
            realtimeSender,
            trip,
            booking,
            driverId,
            passengerId);
    }

    private static SendChatMessageRequestDto MessageRequest(string text) =>
        new()
        {
            ClientMessageId = Guid.NewGuid(),
            MessageText = text
        };

    private sealed record ChatTestContext(
        TestUnitOfWork UnitOfWork,
        BookingChatService Service,
        RecordingChatRealtimeSender RealtimeSender,
        Trip Trip,
        TripBooking Booking,
        Guid DriverId,
        Guid PassengerId);

    private sealed class RecordingChatRealtimeSender : IChatRealtimeSender
    {
        public Guid DriverId { get; private set; }
        public Guid PassengerId { get; private set; }
        public ChatMessageResponseDto? Message { get; private set; }
        public int SendCount { get; private set; }

        public Task SendAsync(
            Guid driverId,
            Guid passengerId,
            ChatMessageResponseDto message,
            CancellationToken cancellationToken = default)
        {
            DriverId = driverId;
            PassengerId = passengerId;
            Message = message;
            SendCount++;
            return Task.CompletedTask;
        }
    }
}
