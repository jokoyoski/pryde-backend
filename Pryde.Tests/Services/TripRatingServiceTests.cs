using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;
using System.Text.Json;

namespace Pryde.Tests.Services;

public class TripRatingServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PassengerCanRateDriverForCompletedBooking()
    {
        var context = Context();

        var result = await context.Service.CreateAsync(
            context.Booking.Id,
            context.PassengerId,
            new TripRatingRequestDto
            {
                Value = 5,
                Comment = "  Safe and punctual.  "
            });

        Assert.Equal(context.DriverId, result.RatedUserId);
        Assert.Equal("Safe and punctual.", result.Comment);
        Assert.Single(context.UnitOfWork.TripRatingRepository.Items);
        var notification = Assert.Single(
            context.UnitOfWork.NotificationRepository.Items);
        Assert.Equal(NotificationType.RatingReceived, notification.Type);
        Assert.Equal(context.DriverId, notification.UserId);
    }

    [Fact]
    public async Task DriverCanRatePassengerForCompletedBooking()
    {
        var context = Context();

        var result = await context.Service.CreateAsync(
            context.Booking.Id,
            context.DriverId,
            new TripRatingRequestDto { Value = 4 });

        Assert.Equal(context.PassengerId, result.RatedUserId);
        Assert.Null(result.Comment);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task RatingMustBeBetweenOneAndFive(int value)
    {
        var context = Context();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.CreateAsync(
                context.Booking.Id,
                context.PassengerId,
                new TripRatingRequestDto { Value = value }));
    }

    [Fact]
    public async Task CommentCannotExceedMaximumLength()
    {
        var context = Context();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.CreateAsync(
                context.Booking.Id,
                context.PassengerId,
                new TripRatingRequestDto
                {
                    Value = 5,
                    Comment = new string('x', 1001)
                }));
    }

    [Fact]
    public async Task SameUserCannotRateSameBookingTwice()
    {
        var context = Context();
        var request = new TripRatingRequestDto { Value = 5 };
        await context.Service.CreateAsync(
            context.Booking.Id,
            context.PassengerId,
            request);

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.CreateAsync(
                context.Booking.Id,
                context.PassengerId,
                request));
    }

    [Fact]
    public async Task UnrelatedUserCannotRateBookingParticipant()
    {
        var context = Context();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            context.Service.CreateAsync(
                context.Booking.Id,
                Guid.NewGuid(),
                new TripRatingRequestDto { Value = 5 }));
    }

    [Theory]
    [InlineData(TripStatus.InProgress, BookingStatus.Approved)]
    [InlineData(TripStatus.Completed, BookingStatus.Approved)]
    [InlineData(TripStatus.InProgress, BookingStatus.Completed)]
    public async Task BothTripAndBookingMustBeCompleted(
        TripStatus tripStatus,
        BookingStatus bookingStatus)
    {
        var context = Context();
        context.Booking.Trip.Status = tripStatus;
        context.Booking.Status = bookingStatus;

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.CreateAsync(
                context.Booking.Id,
                context.PassengerId,
                new TripRatingRequestDto { Value = 5 }));
    }

    [Fact]
    public async Task RatingAtWindowBoundaryIsAccepted()
    {
        var context = Context(Now.AddHours(-24));

        var result = await context.Service.CreateAsync(
            context.Booking.Id,
            context.PassengerId,
            new TripRatingRequestDto { Value = 5 });

        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task RatingAfterWindowIsRejected()
    {
        var context = Context(Now.AddHours(-24).AddTicks(-1));

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.CreateAsync(
                context.Booking.Id,
                context.PassengerId,
                new TripRatingRequestDto { Value = 5 }));
    }

    [Fact]
    public async Task SummaryReturnsAverageAndCount()
    {
        var context = Context();
        context.UnitOfWork.TripRatingRepository.Items.AddRange(
            new TripRating
            {
                BookingId = Guid.NewGuid(),
                RaterId = Guid.NewGuid(),
                RatedUserId = context.DriverId,
                Value = 4
            },
            new TripRating
            {
                BookingId = Guid.NewGuid(),
                RaterId = Guid.NewGuid(),
                RatedUserId = context.DriverId,
                Value = 5
            });

        var result = await context.Service.GetSummaryAsync(
            context.DriverId);

        Assert.Equal(4.5, result.AverageRating);
        Assert.Equal(2, result.RatingCount);
    }

    [Fact]
    public async Task AdminRatingsReturnCommentsAndRaterDetails()
    {
        var context = AdminContext();

        var result = await context.Service.AdminGetByUserIdAsync(
            context.UserId,
            new AdminUserRatingsRequestDto());

        var newest = result.Items[0];
        Assert.Equal("Latest comment", newest.Comment);
        Assert.Equal("Dayo Passenger", newest.RaterName);
        Assert.Equal(RoleType.Passenger.ToString(), newest.RaterRole);
        Assert.Equal(context.UserId, newest.RatedUserId);
        Assert.NotEqual(Guid.Empty, newest.BookingId);
        Assert.NotEqual(Guid.Empty, newest.TripId);
        Assert.Equal("Lagos Island", newest.TripOrigin);
        Assert.Equal("Ikeja", newest.TripDestination);
    }

    [Fact]
    public async Task AdminAggregateUsesAllReceivedRatings()
    {
        var context = AdminContext();

        var result = await context.Service.AdminGetByUserIdAsync(
            context.UserId,
            new AdminUserRatingsRequestDto { PageSize = 1 });

        Assert.Single(result.Items);
        Assert.Equal(4, result.AverageRating);
        Assert.Equal(3, result.TotalRatings);
        Assert.Equal(80, result.RatingPercentage);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task AdminRatingsArePagedNewestFirst()
    {
        var context = AdminContext();

        var firstPage = await context.Service.AdminGetByUserIdAsync(
            context.UserId,
            new AdminUserRatingsRequestDto
            {
                PageNumber = 1,
                PageSize = 2
            });
        var secondPage = await context.Service.AdminGetByUserIdAsync(
            context.UserId,
            new AdminUserRatingsRequestDto
            {
                PageNumber = 2,
                PageSize = 2
            });

        Assert.Equal(2, firstPage.Items.Count);
        Assert.Single(secondPage.Items);
        Assert.True(
            firstPage.Items[0].CreatedAt >
            firstPage.Items[1].CreatedAt);
        Assert.True(
            firstPage.Items[1].CreatedAt >
            secondPage.Items[0].CreatedAt);
        Assert.Equal(2, firstPage.TotalPages);
    }

    [Fact]
    public async Task AdminRatingsReturnNotFoundForUnknownUser()
    {
        var context = AdminContext();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            context.Service.AdminGetByUserIdAsync(
                Guid.NewGuid(),
                new AdminUserRatingsRequestDto()));
    }

    [Fact]
    public async Task PublicSummaryContainsOnlyAggregateInformation()
    {
        var context = AdminContext();

        var result = await context.Service.GetSummaryAsync(
            context.UserId);
        var json = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("comment", json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rater", json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bookingId", json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tripId", json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("items", json,
            StringComparison.OrdinalIgnoreCase);
    }

    private static RatingContext Context(
        DateTimeOffset? completedAt = null)
    {
        var unitOfWork = new TestUnitOfWork();
        var driver = new User { Id = Guid.NewGuid() };
        var passenger = new User { Id = Guid.NewGuid() };
        unitOfWork.UserRepository.Items.AddRange(driver, passenger);
        var trip = new Trip
        {
            DriverId = driver.Id,
            Driver = driver,
            Status = TripStatus.Completed,
            CompletedAt = (completedAt ?? Now.AddHours(-1)).UtcDateTime
        };
        var booking = new TripBooking
        {
            TripId = trip.Id,
            Trip = trip,
            PassengerId = passenger.Id,
            Passenger = passenger,
            Status = BookingStatus.Completed
        };
        trip.Bookings.Add(booking);
        unitOfWork.TripRepository.Items.Add(trip);
        unitOfWork.TripBookingRepository.Items.Add(booking);
        var service = new TripRatingService(
            unitOfWork,
            new NotificationService(unitOfWork),
            new FixedTimeProvider(Now));
        return new RatingContext(
            unitOfWork,
            service,
            booking,
            driver.Id,
            passenger.Id);
    }

    private static AdminRatingContext AdminContext()
    {
        var unitOfWork = new TestUnitOfWork();
        var user = new User { Id = Guid.NewGuid() };
        unitOfWork.UserRepository.Items.Add(user);
        var now = Now.UtcDateTime;
        AddReceivedRating(
            unitOfWork,
            user,
            3,
            "Old comment",
            now.AddMinutes(-3));
        AddReceivedRating(
            unitOfWork,
            user,
            4,
            null,
            now.AddMinutes(-2));
        AddReceivedRating(
            unitOfWork,
            user,
            5,
            "Latest comment",
            now.AddMinutes(-1));
        var service = new TripRatingService(
            unitOfWork,
            new NotificationService(unitOfWork));
        return new AdminRatingContext(
            unitOfWork,
            service,
            user.Id);
    }

    private static void AddReceivedRating(
        TestUnitOfWork unitOfWork,
        User ratedUser,
        int value,
        string? comment,
        DateTime createdAt)
    {
        var rater = new User
        {
            Id = Guid.NewGuid(),
            Profile = new Profile
            {
                FirstName = "Dayo",
                LastName = "Passenger"
            }
        };
        var trip = new Trip
        {
            DriverId = ratedUser.Id,
            Driver = ratedUser,
            OriginAddress = "Lagos Island",
            DestinationAddress = "Ikeja"
        };
        var booking = new TripBooking
        {
            TripId = trip.Id,
            Trip = trip,
            PassengerId = rater.Id,
            Passenger = rater
        };
        var rating = new TripRating
        {
            BookingId = booking.Id,
            Booking = booking,
            RaterId = rater.Id,
            Rater = rater,
            RatedUserId = ratedUser.Id,
            RatedUser = ratedUser,
            Value = value,
            Comment = comment,
            CreatedAt = createdAt
        };
        unitOfWork.UserRepository.Items.Add(rater);
        unitOfWork.TripRepository.Items.Add(trip);
        unitOfWork.TripBookingRepository.Items.Add(booking);
        unitOfWork.TripRatingRepository.Items.Add(rating);
    }

    private sealed record RatingContext(
        TestUnitOfWork UnitOfWork,
        TripRatingService Service,
        TripBooking Booking,
        Guid DriverId,
        Guid PassengerId);

    private sealed record AdminRatingContext(
        TestUnitOfWork UnitOfWork,
        TripRatingService Service,
        Guid UserId);

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
