using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class TripBookingServiceTests
{
    [Fact]
    public async Task PassengerCanRequestAnOpenTrip()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var passengerId = Guid.NewGuid();

        var result = await new TripBookingService(unitOfWork).CreateAsync(passengerId, trip.Id);

        Assert.Equal(BookingStatus.Pending, result.Status);
        Assert.Equal(
            WorkflowNextAction.AwaitDriverDecision,
            result.NextAction);
        Assert.Equal(WorkflowActor.Driver, result.RequiredActor);
        Assert.Equal(2375m, result.SeatPrice);
        Assert.Equal(118.75m, result.ServiceCharge);
        Assert.Equal(2493.75m, result.TotalAmount);
        Assert.Equal(2, trip.AvailableSeats);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task DriverCannotBookTheirOwnTrip()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new TripBookingService(unitOfWork).CreateAsync(driverId, trip.Id));
    }

    [Theory]
    [InlineData(BookingStatus.Pending)]
    [InlineData(BookingStatus.Approved)]
    public async Task DuplicatePendingOrApprovedBookingIsRejected(BookingStatus status)
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var passengerId = Guid.NewGuid();
        unitOfWork.TripBookingRepository.Items.Add(TestData.Booking(trip, passengerId, status));

        await Assert.ThrowsAsync<ConflictException>(() =>
            new TripBookingService(unitOfWork).CreateAsync(passengerId, trip.Id));
    }

    [Fact]
    public async Task BookingRequestAfterCutoffIsRejected()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        trip.DepartureTime = DateTime.UtcNow.AddHours(4);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new TripBookingService(unitOfWork).CreateAsync(Guid.NewGuid(), trip.Id));
    }

    [Fact]
    public async Task DriverCanApprovePendingRequest()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var booking = AddBooking(unitOfWork, trip);

        var result = await new TripBookingService(unitOfWork).ApproveAsync(booking.Id, driverId);

        Assert.Equal(BookingStatus.Approved, result.Status);
        Assert.Equal(
            WorkflowNextAction.PayForBooking,
            result.NextAction);
        Assert.Equal(WorkflowActor.Passenger, result.RequiredActor);
        Assert.NotNull(result.ApprovedAt);
        Assert.Equal(1, trip.AvailableSeats);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ApprovalDecrementsAvailableSeatsExactlyOnce()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var booking = AddBooking(unitOfWork, trip);
        var service = new TripBookingService(unitOfWork);

        await service.ApproveAsync(booking.Id, driverId);
        await Assert.ThrowsAsync<ConflictException>(() => service.ApproveAsync(booking.Id, driverId));

        Assert.Equal(1, trip.AvailableSeats);
    }

    [Fact]
    public async Task ApprovalFailsWhenNoSeatsRemain()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle, 0);
        var booking = AddBooking(unitOfWork, trip);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new TripBookingService(unitOfWork).ApproveAsync(booking.Id, driverId));
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public async Task UnrelatedDriverCannotApproveRequest()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var booking = AddBooking(unitOfWork, trip);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new TripBookingService(unitOfWork).ApproveAsync(booking.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task DecliningDoesNotDecrementSeats()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var booking = AddBooking(unitOfWork, trip);

        var result = await new TripBookingService(unitOfWork).DeclineAsync(booking.Id, driverId);

        Assert.Equal(BookingStatus.Declined, result.Status);
        Assert.Equal(WorkflowNextAction.None, result.NextAction);
        Assert.Equal(WorkflowActor.None, result.RequiredActor);
        Assert.Equal(2, trip.AvailableSeats);
    }

    [Fact]
    public async Task CancellingApprovedBookingRestoresOneSeat()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext(4);
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle, 1);
        var passengerId = Guid.NewGuid();
        var booking = AddBooking(unitOfWork, trip, passengerId, BookingStatus.Approved);

        var result = await new TripBookingService(unitOfWork).CancelAsync(booking.Id, passengerId);

        Assert.Equal(BookingStatus.Cancelled, result.Status);
        Assert.Equal(2, trip.AvailableSeats);
    }

    [Fact]
    public async Task PassengerCannotCancelAnotherPassengersBooking()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var booking = AddBooking(unitOfWork, trip, Guid.NewGuid(), BookingStatus.Approved);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new TripBookingService(unitOfWork).CancelAsync(booking.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task PassengerCanViewTheirBookings()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        var passengerId = Guid.NewGuid();
        AddBooking(unitOfWork, trip, passengerId);

        var result = await new TripBookingService(unitOfWork).GetMineAsync(passengerId);

        Assert.Single(result);
    }

    [Fact]
    public async Task DriverCanViewConfirmedPassengers()
    {
        var (unitOfWork, driverId, vehicle) = TestData.CreateDriverContext();
        var trip = AddOpenTrip(unitOfWork, driverId, vehicle);
        AddBooking(unitOfWork, trip, status: BookingStatus.Approved);

        var result = await new TripBookingService(unitOfWork).GetConfirmedPassengersAsync(trip.Id, driverId);

        Assert.Single(result);
        Assert.Equal(BookingStatus.Approved, result[0].Status);
    }

    private static Pryde.Domain.Entities.Trip AddOpenTrip(
        TestUnitOfWork unitOfWork,
        Guid driverId,
        Pryde.Domain.Entities.Vehicle vehicle,
        int availableSeats = 2)
    {
        var trip = TestData.OpenTrip(driverId, vehicle, availableSeats);
        unitOfWork.TripRepository.Items.Add(trip);
        return trip;
    }

    private static Pryde.Domain.Entities.TripBooking AddBooking(
        TestUnitOfWork unitOfWork,
        Pryde.Domain.Entities.Trip trip,
        Guid? passengerId = null,
        BookingStatus status = BookingStatus.Pending)
    {
        var booking = TestData.Booking(trip, passengerId ?? Guid.NewGuid(), status);
        unitOfWork.TripBookingRepository.Items.Add(booking);
        return booking;
    }
}
