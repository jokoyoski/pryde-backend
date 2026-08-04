using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pryde.Api.Controllers.V1;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Api;

public class TripRatingsControllerTests
{
    [Fact]
    public void ControllerRequiresAuthentication()
    {
        Assert.NotNull(typeof(TripRatingsController)
            .GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task CreateUsesAuthenticatedUserAsRater()
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
            CompletedAt = DateTime.UtcNow.AddHours(-1)
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
        var controller = new TripRatingsController(
            new TripRatingService(
                unitOfWork,
                new NotificationService(unitOfWork)))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            [new Claim(
                                ClaimTypes.NameIdentifier,
                                passenger.Id.ToString())],
                            "Test"))
                }
            }
        };

        var action = await controller.Create(
            booking.Id,
            new TripRatingRequestDto { Value = 5 },
            CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var response = Assert.IsType<TripRatingResponseDto>(created.Value);
        Assert.Equal(passenger.Id, response.RaterId);
        Assert.Equal(driver.Id, response.RatedUserId);
    }
}
