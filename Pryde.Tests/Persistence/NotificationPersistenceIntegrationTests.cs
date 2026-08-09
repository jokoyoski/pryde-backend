using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Constants;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Implementations;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Service.Interface;

namespace Pryde.Tests.Persistence;

public class NotificationPersistenceIntegrationTests
{
    [DatabaseFact]
    public async Task AdminGetAllAsyncFiltersByUserIdBeforeProjection()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddPersistence(configuration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider
            .GetRequiredService<PrydeDbContext>();
        var title =
            $"Admin notification filter {Guid.NewGuid():N}";

        try
        {
            var userIds = await context.Users
                .AsNoTracking()
                .OrderBy(user => user.Id)
                .Select(user => user.Id)
                .Take(2)
                .ToListAsync();
            Assert.Equal(2, userIds.Count);

            var expected = new Notification
            {
                UserId = userIds[0],
                Type = NotificationType.SystemAnnouncement,
                Title = title,
                Message = "Expected notification"
            };
            var other = new Notification
            {
                UserId = userIds[1],
                Type = NotificationType.SystemAnnouncement,
                Title = title,
                Message = "Other notification"
            };
            context.Notifications.AddRange(expected, other);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var repository = new NotificationRepository(context);
            var result = await repository.AdminGetAllAsync(
                1,
                100,
                userIds[0],
                null,
                null,
                null,
                null);

            Assert.Contains(
                result.Items,
                notification => notification.Id == expected.Id);
            Assert.DoesNotContain(
                result.Items,
                notification => notification.Id == other.Id);
        }
        finally
        {
            await context.Notifications
                .Where(notification =>
                    notification.Title == title)
                .ExecuteDeleteAsync();
        }
    }

    [DatabaseFact]
    public async Task CreatedNotificationIsPersistedAndImmediatelyReadable()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddPersistence(configuration);
        services.AddLogging();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<INotificationRealtimeSender,
            NoOpNotificationRealtimeSender>();

        await using var provider = services.BuildServiceProvider();
        Guid? createdNotificationId = null;
        var broadcastCount = 0;
        var notificationTitle =
            $"Notification feed integration {Guid.NewGuid():N}";
        var broadcastTitle =
            $"Notification integration broadcast {Guid.NewGuid():N}";

        try
        {
            Guid userId;
            string deduplicationKey;
            await using (var createScope =
                         provider.CreateAsyncScope())
            {
                var context = createScope.ServiceProvider
                    .GetRequiredService<PrydeDbContext>();
                var service = createScope.ServiceProvider
                    .GetRequiredService<INotificationService>();
                userId = await context.Users
                    .AsNoTracking()
                    .Where(user =>
                        user.Status != UserStatus.Suspended &&
                        user.Status != UserStatus.Deactivated &&
                        !user.UserRoles.Any(userRole =>
                            userRole.Role.Name == RoleNames.Admin ||
                            userRole.Role.Name ==
                                RoleNames.SuperAdmin) &&
                        user.UserRoles.Any(userRole =>
                            userRole.Role.Name == RoleNames.Driver ||
                            userRole.Role.Name ==
                                RoleNames.Passenger))
                    .OrderBy(user => user.Id)
                    .Select(user => user.Id)
                    .FirstAsync();
                deduplicationKey =
                    $"notification-integration-{Guid.NewGuid():N}";

                var created = await service.CreateAsync(
                    new CreateNotificationRequest
                    {
                        UserId = userId,
                        Type = NotificationType.SystemAnnouncement,
                        Title = notificationTitle,
                        Message = "This row is deleted after verification.",
                        DeduplicationKey = deduplicationKey
                    });
                createdNotificationId = created.Id;
            }

            await using (var readScope =
                         provider.CreateAsyncScope())
            {
                var context = readScope.ServiceProvider
                    .GetRequiredService<PrydeDbContext>();
                var service = readScope.ServiceProvider
                    .GetRequiredService<INotificationService>();
                var stored = await context.Notifications
                    .AsNoTracking()
                    .SingleAsync(notification =>
                        notification.Id ==
                        createdNotificationId.Value);
                var feed = await service.GetMineAsync(
                    userId,
                    new UserNotificationsRequestDto
                    {
                        PageNumber = 1,
                        PageSize = 100
                    });

                Assert.Equal(
                    deduplicationKey,
                    stored.DeduplicationKey);
                Assert.Equal(userId, stored.UserId);
                Assert.Equal(notificationTitle, stored.Title);
                Assert.NotEmpty(feed.Items);
                Assert.Equal(
                    createdNotificationId.Value,
                    feed.Items[0].Id);
                Assert.Equal(
                    notificationTitle,
                    feed.Items[0].Title);
                Assert.Equal(
                    feed.Items
                        .OrderByDescending(notification =>
                            notification.CreatedAt)
                        .ThenByDescending(notification =>
                            notification.Id)
                        .Select(notification => notification.Id),
                    feed.Items.Select(notification =>
                        notification.Id));

                var broadcast = await service.BroadcastAsync(
                    new AdminBroadcastNotificationRequestDto
                    {
                        Title = broadcastTitle,
                        Message = "These rows are deleted after verification.",
                        Audience = NotificationAudience.All
                    });

                Assert.True(broadcast.Count > 0);
                broadcastCount = broadcast.Count;
            }

            await using (var verifyScope =
                         provider.CreateAsyncScope())
            {
                var context = verifyScope.ServiceProvider
                    .GetRequiredService<PrydeDbContext>();
                var persistedBroadcastCount = await context.Notifications
                    .AsNoTracking()
                    .CountAsync(notification =>
                        notification.Title == broadcastTitle);
                var expectedRecipientCount = await context.Users
                    .AsNoTracking()
                    .CountAsync(user =>
                        user.Status != UserStatus.Suspended &&
                        user.Status != UserStatus.Deactivated &&
                        !user.UserRoles.Any(userRole =>
                            userRole.Role.Name == "Admin" ||
                            userRole.Role.Name == "SuperAdmin") &&
                        user.UserRoles.Any(userRole =>
                            userRole.Role.Name == "Driver" ||
                            userRole.Role.Name == "Passenger"));

                Assert.Equal(
                    expectedRecipientCount,
                    persistedBroadcastCount);
                Assert.Equal(
                    broadcastCount,
                    persistedBroadcastCount);
            }
        }
        finally
        {
            if (createdNotificationId.HasValue)
            {
                await using var cleanupScope =
                    provider.CreateAsyncScope();
                var cleanupContext = cleanupScope.ServiceProvider
                    .GetRequiredService<PrydeDbContext>();
                await cleanupContext.Notifications
                    .Where(notification =>
                        notification.Id ==
                            createdNotificationId.Value ||
                        notification.Title == broadcastTitle)
                    .ExecuteDeleteAsync();
            }
        }
    }

    [DatabaseFact]
    public async Task BroadcastToDriversIsReturnedBySelectedPendingDriversFeed()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddPersistence(configuration);
        services.AddLogging();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<INotificationRealtimeSender,
            NoOpNotificationRealtimeSender>();

        await using var provider = services.BuildServiceProvider();
        var userId = Guid.NewGuid();
        var title = $"Driver broadcast integration {Guid.NewGuid():N}";

        try
        {
            await using (var createScope = provider.CreateAsyncScope())
            {
                var context = createScope.ServiceProvider
                    .GetRequiredService<PrydeDbContext>();
                var driverRoleId = await context.Roles
                    .Where(role => role.Name == RoleNames.Driver)
                    .Select(role => role.Id)
                    .SingleAsync();
                var suffix = userId.ToString("N");

                context.Users.Add(new User
                {
                    Id = userId,
                    Email = $"broadcast-{suffix}@integration.test",
                    PhoneNumber = $"+{suffix[..15]}",
                    PasswordHash = "integration-test",
                    IsEmailVerified = true,
                    Status = UserStatus.Pending
                });
                context.UserRoles.Add(new UserRole
                {
                    UserId = userId,
                    RoleId = driverRoleId
                });
                await context.SaveChangesAsync();
            }

            await using (var broadcastScope = provider.CreateAsyncScope())
            {
                var service = broadcastScope.ServiceProvider
                    .GetRequiredService<INotificationService>();
                var result = await service.BroadcastAsync(
                    new AdminBroadcastNotificationRequestDto
                    {
                        Title = title,
                        Message = "Selected pending driver must receive this broadcast.",
                        Audience = NotificationAudience.Drivers
                    });

                Assert.True(result.Count > 0);
            }

            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var context = verifyScope.ServiceProvider
                    .GetRequiredService<PrydeDbContext>();
                var service = verifyScope.ServiceProvider
                    .GetRequiredService<INotificationService>();
                var stored = await context.Notifications
                    .AsNoTracking()
                    .SingleAsync(notification =>
                        notification.UserId == userId &&
                        notification.Type ==
                            NotificationType.SystemAnnouncement &&
                        notification.Title == title);
                var feed = await service.GetMineAsync(
                    userId,
                    new UserNotificationsRequestDto
                    {
                        PageNumber = 1,
                        PageSize = 100,
                        Type = NotificationType.SystemAnnouncement
                    });

                Assert.Contains(
                    feed.Items,
                    notification =>
                        notification.Id == stored.Id &&
                        notification.Title == title);
            }
        }
        finally
        {
            await using var cleanupScope = provider.CreateAsyncScope();
            var context = cleanupScope.ServiceProvider
                .GetRequiredService<PrydeDbContext>();
            await context.Notifications
                .Where(notification => notification.Title == title)
                .ExecuteDeleteAsync();
            await context.UserRoles
                .Where(userRole => userRole.UserId == userId)
                .ExecuteDeleteAsync();
            await context.Users
                .Where(user => user.Id == userId)
                .ExecuteDeleteAsync();
        }
    }

    private sealed class NoOpNotificationRealtimeSender
        : INotificationRealtimeSender
    {
        public Task SendAsync(
            Guid userId,
            NotificationResponseDto notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class DatabaseFactAttribute : FactAttribute
    {
        public DatabaseFactAttribute()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable(
                        "PRYDE_RUN_DATABASE_TESTS"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                Skip = "Set PRYDE_RUN_DATABASE_TESTS=true to run database integration tests.";
            }
        }
    }
}
