using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Service.Interface;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class NotificationServiceTests
{
    [Fact]
    public async Task NotificationCreationWorks()
    {
        var unitOfWork = Context(out var user);
        var service = new NotificationService(unitOfWork);

        var result = await service.CreateAsync(
            Request(
                user.Id,
                NotificationType.BookingApproved));

        Assert.Equal(
            NotificationType.BookingApproved,
            result.Type);
        Assert.False(result.IsRead);
        Assert.Single(
            unitOfWork.NotificationRepository.Items);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task DuplicateDeduplicationKeyReturnsExistingNotification()
    {
        var unitOfWork = Context(out var user);
        var service = new NotificationService(unitOfWork);
        var request = Request(
            user.Id,
            NotificationType.BookingApproved,
            "booking-approved-1");

        var first = await service.CreateAsync(request);
        var second = await service.CreateAsync(request);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(
            unitOfWork.NotificationRepository.Items);
    }

    [Fact]
    public async Task UserListingIsOwnedPagedFilteredAndNewestFirst()
    {
        var unitOfWork = Context(out var user);
        var otherUser = AddUser(
            unitOfWork,
            "other@test.local");
        var old = AddNotification(
            unitOfWork,
            user.Id,
            NotificationType.BookingApproved,
            DateTime.UtcNow.AddMinutes(-2),
            false);
        var newest = AddNotification(
            unitOfWork,
            user.Id,
            NotificationType.BookingApproved,
            DateTime.UtcNow.AddMinutes(-1),
            false);
        AddNotification(
            unitOfWork,
            user.Id,
            NotificationType.KycApproved,
            DateTime.UtcNow,
            true);
        AddNotification(
            unitOfWork,
            otherUser.Id,
            NotificationType.BookingApproved,
            DateTime.UtcNow.AddMinutes(1),
            false);
        var service = new NotificationService(unitOfWork);

        var result = await service.GetMineAsync(
            user.Id,
            new UserNotificationsRequestDto
            {
                PageNumber = 1,
                PageSize = 1,
                IsRead = false,
                Type = NotificationType.BookingApproved
            });

        Assert.Single(result.Items);
        Assert.Equal(newest.Id, result.Items[0].Id);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.DoesNotContain(
            result.Items,
            item => item.Id == old.Id);
    }

    [Fact]
    public async Task PageSizeUsesExistingCap()
    {
        var unitOfWork = Context(out var user);
        var request = new UserNotificationsRequestDto
        {
            PageSize = 500
        };

        var result = await new NotificationService(unitOfWork)
            .GetMineAsync(user.Id, request);

        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    public async Task MarkAsReadWorksAndIsIdempotent()
    {
        var unitOfWork = Context(out var user);
        var notification = AddNotification(
            unitOfWork,
            user.Id,
            NotificationType.BookingRequested,
            DateTime.UtcNow,
            false);
        var service = new NotificationService(unitOfWork);

        var first = await service.MarkAsReadAsync(
            notification.Id,
            user.Id);
        var firstReadAt = first.ReadAt;
        var second = await service.MarkAsReadAsync(
            notification.Id,
            user.Id);

        Assert.True(first.IsRead);
        Assert.NotNull(firstReadAt);
        Assert.Equal(firstReadAt, second.ReadAt);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task UserCannotMarkAnotherUsersNotificationAsRead()
    {
        var unitOfWork = Context(out var user);
        var otherUser = AddUser(
            unitOfWork,
            "other@test.local");
        var notification = AddNotification(
            unitOfWork,
            otherUser.Id,
            NotificationType.BookingRequested,
            DateTime.UtcNow,
            false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new NotificationService(unitOfWork)
                .MarkAsReadAsync(
                    notification.Id,
                    user.Id));

        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task MarkAllAsReadOnlyAffectsAuthenticatedUser()
    {
        var unitOfWork = Context(out var user);
        var otherUser = AddUser(
            unitOfWork,
            "other@test.local");
        var first = AddNotification(
            unitOfWork,
            user.Id,
            NotificationType.BookingRequested,
            DateTime.UtcNow,
            false);
        var second = AddNotification(
            unitOfWork,
            user.Id,
            NotificationType.KycApproved,
            DateTime.UtcNow,
            false);
        var other = AddNotification(
            unitOfWork,
            otherUser.Id,
            NotificationType.BookingRequested,
            DateTime.UtcNow,
            false);

        var result = await new NotificationService(unitOfWork)
            .MarkAllAsReadAsync(user.Id);

        Assert.Equal(2, result.Count);
        Assert.True(first.IsRead);
        Assert.True(second.IsRead);
        Assert.False(other.IsRead);
    }

    [Fact]
    public async Task UnreadCountIsCorrect()
    {
        var unitOfWork = Context(out var user);
        AddNotification(
            unitOfWork,
            user.Id,
            NotificationType.BookingRequested,
            DateTime.UtcNow,
            false);
        AddNotification(
            unitOfWork,
            user.Id,
            NotificationType.BookingApproved,
            DateTime.UtcNow,
            true);

        var result = await new NotificationService(unitOfWork)
            .GetUnreadCountAsync(user.Id);

        Assert.Equal(1, result.Count);
    }

    [Fact]
    public async Task AdminCanFilterAllWithoutChangingReadState()
    {
        var unitOfWork = Context(out var user);
        var otherUser = AddUser(
            unitOfWork,
            "other@test.local");
        var matching = AddNotification(
            unitOfWork,
            user.Id,
            NotificationType.KycApproved,
            DateTime.UtcNow.AddMinutes(-1),
            false);
        AddNotification(
            unitOfWork,
            otherUser.Id,
            NotificationType.BookingRequested,
            DateTime.UtcNow,
            true);
        var service = new NotificationService(unitOfWork);

        var result = await service.AdminGetAllAsync(
            new AdminNotificationsRequestDto
            {
                UserId = user.Id,
                Type = NotificationType.KycApproved,
                IsRead = false,
                CreatedFrom = DateTime.UtcNow.AddHours(-1),
                CreatedTo = DateTime.UtcNow.AddHours(1)
            });
        var detail = await service.AdminGetByIdAsync(
            matching.Id);

        Assert.Single(result.Items);
        Assert.Equal(user.Email, detail.RecipientEmail);
        Assert.False(matching.IsRead);
        Assert.Null(matching.ReadAt);
    }

    [Fact]
    public void DeduplicationKeyIsNotExposed()
    {
        Assert.Null(typeof(NotificationResponseDto)
            .GetProperty("DeduplicationKey"));
        Assert.Null(typeof(AdminNotificationResponseDto)
            .GetProperty("DeduplicationKey"));
    }

    private static TestUnitOfWork Context(
        out User user)
    {
        var unitOfWork = new TestUnitOfWork();
        user = AddUser(
            unitOfWork,
            "user@test.local");
        return unitOfWork;
    }

    private static User AddUser(
        TestUnitOfWork unitOfWork,
        string email)
    {
        var user = new User
        {
            Email = email,
            PhoneNumber = "08000000000",
            Profile = new Profile
            {
                FirstName = "Test",
                LastName = "User"
            }
        };
        user.Profile.UserId = user.Id;
        user.Profile.User = user;
        unitOfWork.UserRepository.Items.Add(user);
        return user;
    }

    private static Notification AddNotification(
        TestUnitOfWork unitOfWork,
        Guid userId,
        NotificationType type,
        DateTime createdAt,
        bool isRead)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = "Title",
            Message = "Message",
            CreatedAt = createdAt,
            IsRead = isRead,
            ReadAt = isRead ? createdAt : null
        };
        unitOfWork.NotificationRepository.Items.Add(
            notification);
        return notification;
    }

    private static CreateNotificationRequest Request(
        Guid userId,
        NotificationType type,
        string? deduplicationKey = null)
    {
        return new CreateNotificationRequest
        {
            UserId = userId,
            Type = type,
            Title = "Notification title",
            Message = "Notification message",
            DeduplicationKey = deduplicationKey
        };
    }
}
