using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pryde.Api.Controllers.V1;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Api;

public class NotificationControllerTests
{
    [Fact]
    public async Task GetMineReturnsOnlyAuthenticatedUsersNotifications()
    {
        var unitOfWork = new TestUnitOfWork();
        var userId = Guid.NewGuid();
        var ownNotification = CreateNotification(userId);
        unitOfWork.NotificationRepository.Items.AddRange(
            ownNotification,
            CreateNotification(Guid.NewGuid()));
        var controller = CreateController(unitOfWork, userId);

        var result = await controller.GetMine(
            new UserNotificationsRequestDto(),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PagedResponseDto<NotificationResponseDto>>(
            ok.Value);
        Assert.Single(response.Items);
        Assert.Equal(ownNotification.Id, response.Items[0].Id);
    }

    [Fact]
    public async Task MarkAsReadCannotModifyAnotherUsersNotification()
    {
        var unitOfWork = new TestUnitOfWork();
        var notification = CreateNotification(Guid.NewGuid());
        unitOfWork.NotificationRepository.Items.Add(notification);
        var controller = CreateController(
            unitOfWork,
            Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            controller.MarkAsRead(
                notification.Id,
                CancellationToken.None));

        Assert.False(notification.IsRead);
    }

    private static NotificationsController CreateController(
        TestUnitOfWork unitOfWork,
        Guid userId)
    {
        return new NotificationsController(
            new NotificationService(unitOfWork))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            [
                                new Claim(
                                    ClaimTypes.NameIdentifier,
                                    userId.ToString())
                            ],
                            "Test"))
                }
            }
        };
    }

    private static Notification CreateNotification(Guid userId)
    {
        return new Notification
        {
            UserId = userId,
            Type = NotificationType.BookingRequested,
            Title = "Title",
            Message = "Message",
            CreatedAt = DateTime.UtcNow
        };
    }
}
