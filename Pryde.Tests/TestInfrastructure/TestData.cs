using Microsoft.Extensions.Options;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;

namespace Pryde.Tests.TestInfrastructure;

internal static class TestData
{
    public static readonly PricingSettings Pricing = new()
    {
        BaseFare = 500m,
        PerKmRate = 700m,
        PerMinuteRate = 100m,
        MinimumFare = 5000m,
        ServiceChargePercent = 5m,
        PickupRadiusKm = 2
    };

    public static (TestUnitOfWork UnitOfWork, Guid DriverId, Vehicle Vehicle) CreateDriverContext(int capacity = 4)
    {
        var unitOfWork = new TestUnitOfWork();
        var driverId = Guid.NewGuid();
        var profile = new Profile { UserId = driverId, FirstName = "Ada", LastName = "Driver" };
        var driver = new User { Id = driverId, Profile = profile };
        var vehicle = new Vehicle
        {
            UserId = driverId,
            Capacity = capacity,
            IsActive = true,
            OnboardingStatus = VehicleOnboardingStatus.Approved,
            LicensePlateNumber = "PRYDE-01"
        };
        unitOfWork.UserRoleRepository.Items.Add(new UserRole
        {
            UserId = driverId,
            Role = new Role { Name = RoleType.Driver.ToString() }
        });
        unitOfWork.KycVerificationRepository.Items.Add(new KycVerification
        {
            UserId = driverId,
            Status = KycStatus.Approved
        });
        unitOfWork.VehicleRepository.Items.Add(vehicle);
        unitOfWork.ProfileRepository.Items.Add(profile);
        unitOfWork.TripRepository.DefaultDriver = driver;
        unitOfWork.TripRepository.DefaultVehicle = vehicle;
        return (unitOfWork, driverId, vehicle);
    }

    public static TripService CreateTripService(
        TestUnitOfWork unitOfWork,
        TripSettings? tripSettings = null,
        IRouteMatchingService? routeMatchingService = null) => new(
            unitOfWork,
            new FareCalculator(Options.Create(Pricing)),
            routeMatchingService ?? new RouteMatchingService(),
            Options.Create(Pricing),
            Options.Create(tripSettings ?? new TripSettings()),
            new FinancialService(unitOfWork),
            new NotificationService(unitOfWork));

    public static CreateTripRequestDto ValidTripRequest(Guid vehicleId) => new()
    {
        VehicleId = vehicleId,
        OriginLatitude = 6.5244,
        OriginLongitude = 3.3792,
        OriginAddress = "Lagos Island",
        DestinationLatitude = 6.6018,
        DestinationLongitude = 3.3515,
        DestinationAddress = "Ikeja",
        DistanceKm = 10,
        EstimatedDurationMinutes = 20,
        DepartureTime = DateTime.UtcNow.AddHours(10),
        AvailableSeats = 3,
        AllowLuggage = true,
        BookingWindowMinutes = 15
    };

    public static Trip OpenTrip(Guid driverId, Vehicle vehicle, int availableSeats = 2) => new()
    {
        DriverId = driverId,
        Driver = new User { Id = driverId, Profile = new Profile { FirstName = "Ada", LastName = "Driver" } },
        VehicleId = vehicle.Id,
        Vehicle = vehicle,
        OriginAddress = "Lagos Island",
        OriginLatitude = 6.5244,
        OriginLongitude = 3.3792,
        DestinationAddress = "Ikeja",
        DestinationLatitude = 6.6018,
        DestinationLongitude = 3.3515,
        DistanceKm = 10,
        EstimatedDurationMinutes = 20,
        DepartureTime = DateTime.UtcNow.AddHours(10),
        AvailableSeats = availableSeats,
        BookingWindowMinutes = 15,
        TripFare = 9500m,
        SeatPrice = 2375m,
        ServiceChargePercentage = 5m,
        Status = TripStatus.Scheduled
    };

    public static TripBooking Booking(Trip trip, Guid passengerId, BookingStatus status = BookingStatus.Pending)
    {
        var passenger = new User
        {
            Id = passengerId,
            Profile = new Profile { UserId = passengerId, FirstName = "Pat", LastName = "Passenger" }
        };
        var booking = new TripBooking
        {
            TripId = trip.Id,
            Trip = trip,
            PassengerId = passengerId,
            Passenger = passenger,
            Status = status,
            RequestedAt = DateTime.UtcNow,
            ApprovedAt = status == BookingStatus.Approved
                ? DateTime.UtcNow
                : null,
            PaymentExpiresAt =
                status == BookingStatus.Approved
                    ? DateTime.UtcNow.AddMinutes(15)
                    : null,
            SeatPrice = trip.SeatPrice,
            ServiceCharge = 118.75m,
            TotalAmount = 2493.75m
        };
        trip.Bookings.Add(booking);
        return booking;
    }
}
