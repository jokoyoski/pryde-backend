using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Enums;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class TripServiceTests
{
    [Fact]
    public async Task DriverCanCreateAValidTrip()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var service = TestData.CreateTripService(unitOfWork);

        var result = await service.CreateAsync(driverId, TestData.ValidTripRequest(vehicle.Id));

        Assert.Equal(driverId, result.DriverId);
        Assert.Equal(TripStatus.Scheduled, result.Status);
        Assert.Single(unitOfWork.TripRepository.Items);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task DriverCannotCreateTripWithAnotherDriversVehicle()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        vehicle.UserId = Guid.NewGuid();
        var service = TestData.CreateTripService(unitOfWork);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAsync(driverId, TestData.ValidTripRequest(vehicle.Id)));
    }

    [Fact]
    public async Task AvailableSeatsCannotExceedVehicleCapacity()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext(4);
        var request = TestData.ValidTripRequest(vehicle.Id);
        request.AvailableSeats = 5;

        await Assert.ThrowsAsync<ValidationException>(() =>
            TestData.CreateTripService(unitOfWork).CreateAsync(driverId, request));
    }

    [Fact]
    public async Task FareAndSeatPriceUseConfiguredValuesAndFullVehicleCapacity()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext(4);

        var result = await TestData.CreateTripService(unitOfWork)
            .CreateAsync(driverId, TestData.ValidTripRequest(vehicle.Id));

        Assert.Equal(9500m, result.TripFare);
        Assert.Equal(2375m, result.SeatPrice);
        Assert.Equal(118.75m, result.PassengerServiceCharge);
        Assert.Equal(2493.75m, result.PassengerTotal);
    }

    [Fact]
    public async Task DriverCanCancelTripBeforeItStarts()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = TestData.OpenTrip(driverId, vehicle, 1);
        var booking = TestData.Booking(trip, Guid.NewGuid(), BookingStatus.Approved);
        unitOfWork.TripRepository.Items.Add(trip);
        unitOfWork.TripBookingRepository.Items.Add(booking);

        await TestData.CreateTripService(unitOfWork).CancelAsync(trip.Id, driverId);

        Assert.Equal(TripStatus.Cancelled, trip.Status);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(2, trip.AvailableSeats);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task SearchExcludesCancelledCompletedDepartedFullAndBookingClosedTrips()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var valid = TestData.OpenTrip(driverId, vehicle);
        var cancelled = TestData.OpenTrip(driverId, vehicle); cancelled.Status = TripStatus.Cancelled;
        var completed = TestData.OpenTrip(driverId, vehicle); completed.Status = TripStatus.Completed;
        var departed = TestData.OpenTrip(driverId, vehicle); departed.DepartureTime = DateTime.UtcNow.AddMinutes(-1);
        var full = TestData.OpenTrip(driverId, vehicle, 0);
        var bookingClosed = TestData.OpenTrip(driverId, vehicle); bookingClosed.DepartureTime = DateTime.UtcNow.AddHours(4);
        unitOfWork.TripRepository.Items.AddRange([valid, cancelled, completed, departed, full, bookingClosed]);

        var results = await TestData.CreateTripService(unitOfWork).SearchAsync(new SearchTripsRequestDto());

        var result = Assert.Single(results);
        Assert.Equal(valid.Id, result.TripId);
    }
}
