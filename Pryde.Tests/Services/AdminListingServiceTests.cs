using Pryde.Contracts.RequestModels;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class AdminListingServiceTests
{
    [Fact]
    public async Task UserListingIncludesPassengerAndDriverProfilePhotosAndPreservesNull()
    {
        var unitOfWork = new TestUnitOfWork();
        var passenger = User("passenger.photo@test.local");
        passenger.Profile = new Profile
        {
            FirstName = "Passenger",
            LastName = "Photo",
            ProfilePhotoUrl = "https://media.test/passenger.jpg"
        };
        var passengerRole = new Role { Name = "Passenger" };
        passenger.UserRoles.Add(new UserRole
        {
            User = passenger,
            UserId = passenger.Id,
            Role = passengerRole,
            RoleId = passengerRole.Id
        });
        var driver = User("driver.photo@test.local");
        driver.Profile = new Profile
        {
            FirstName = "Driver",
            LastName = "Photo",
            ProfilePhotoUrl = "https://media.test/driver.jpg"
        };
        var driverRole = new Role { Name = "Driver" };
        driver.UserRoles.Add(new UserRole
        {
            User = driver,
            UserId = driver.Id,
            Role = driverRole,
            RoleId = driverRole.Id
        });
        var noPhoto = User("no.photo@test.local");
        noPhoto.Profile = new Profile
        {
            FirstName = "No",
            LastName = "Photo"
        };
        unitOfWork.AdminListingRepository.Users.AddRange(
            [passenger, driver, noPhoto]);

        var result = await new AdminListingService(unitOfWork)
            .GetUsersAsync(new AdminUsersRequestDto());

        Assert.Equal(
            "https://media.test/passenger.jpg",
            result.Items.Single(item => item.Id == passenger.Id).ProfilePhotoUrl);
        Assert.Equal(
            "https://media.test/driver.jpg",
            result.Items.Single(item => item.Id == driver.Id).ProfilePhotoUrl);
        Assert.Null(result.Items.Single(item => item.Id == noPhoto.Id).ProfilePhotoUrl);
    }

    [Fact]
    public async Task UserListingPaginationWorks()
    {
        var unitOfWork = new TestUnitOfWork();
        for (var i = 0; i < 25; i++)
            unitOfWork.AdminListingRepository.Users.Add(User($"user{i}@test.local"));

        var result = await new AdminListingService(unitOfWork).GetUsersAsync(new AdminUsersRequestDto { PageNumber = 2, PageSize = 10 });

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public async Task UserListingCapsPageSizeAndAppliesRequestedFilters()
    {
        var unitOfWork = new TestUnitOfWork();
        var matching = User("verified.driver@test.local");
        matching.IsEmailVerified = true;
        matching.IsPhoneNumberVerified = true;
        matching.KycVerification = Kyc(matching, KycStatus.Approved);
        var driverRole = new Role { Name = "Driver" };
        matching.UserRoles.Add(new UserRole
        {
            User = matching,
            UserId = matching.Id,
            Role = driverRole,
            RoleId = driverRole.Id
        });
        unitOfWork.AdminListingRepository.Users.Add(matching);
        for (var i = 0; i < 120; i++)
            unitOfWork.AdminListingRepository.Users.Add(User($"other{i}@test.local"));

        var request = new AdminUsersRequestDto
        {
            PageSize = 500,
            Search = "verified.driver",
            Role = "Driver",
            IsActive = true,
            IsEmailVerified = true,
            IsPhoneVerified = true,
            KycStatus = KycStatus.Approved
        };
        var result = await new AdminListingService(unitOfWork).GetUsersAsync(request);

        Assert.Equal(100, result.PageSize);
        Assert.Single(result.Items);
        Assert.Equal(matching.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task KycStatusFilterWorks()
    {
        var unitOfWork = new TestUnitOfWork();
        unitOfWork.AdminListingRepository.Kyc.Add(Kyc(User("pending@test.local"), KycStatus.Pending));
        unitOfWork.AdminListingRepository.Kyc.Add(Kyc(User("approved@test.local"), KycStatus.Approved));

        var result = await new AdminListingService(unitOfWork).GetKycAsync(new AdminKycRequestDto { Status = KycStatus.Pending });

        Assert.Single(result.Items);
        Assert.Equal(KycStatus.Pending, result.Items[0].Status);
    }

    [Fact]
    public async Task VehicleActiveFilterWorks()
    {
        var unitOfWork = new TestUnitOfWork();
        unitOfWork.AdminListingRepository.Vehicles.Add(Vehicle(User("active@test.local"), true));
        unitOfWork.AdminListingRepository.Vehicles.Add(Vehicle(User("inactive@test.local"), false));

        var result = await new AdminListingService(unitOfWork).GetVehiclesAsync(new AdminVehiclesRequestDto { IsActive = true });

        Assert.Single(result.Items);
        Assert.True(result.Items[0].IsActive);
    }

    [Fact]
    public async Task AdminCanViewVehicleDocumentsForAnyOwner()
    {
        var unitOfWork = new TestUnitOfWork();
        var vehicle = Vehicle(User("owner@test.local"), true);
        unitOfWork.AdminListingRepository.VehicleDocuments.Add(new VehicleDocument
        {
            Id = Guid.NewGuid(), VehicleId = vehicle.Id, Vehicle = vehicle,
            DocumentType = VehicleDocumentType.Insurance, DocumentUrl = "https://example.test/document", ExpiryDate = DateTime.UtcNow.AddYears(1)
        });

        var result = await new AdminListingService(unitOfWork).GetVehicleDocumentsAsync(new AdminVehicleDocumentsRequestDto());

        Assert.Single(result.Items);
        Assert.Equal(vehicle.UserId, result.Items[0].OwnerId);
    }

    [Fact]
    public async Task TripAndBookingListingsArePagedAndFiltered()
    {
        var unitOfWork = new TestUnitOfWork();
        var driver = User("driver@test.local");
        var vehicle = Vehicle(driver, true);
        var trip = new Trip
        {
            DriverId = driver.Id,
            Driver = driver,
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            OriginAddress = "Lagos",
            DestinationAddress = "Abuja",
            DepartureTime = DateTime.UtcNow.AddDays(1),
            Status = TripStatus.Scheduled
        };
        var passenger = User("passenger@test.local");
        var booking = new TripBooking
        {
            TripId = trip.Id,
            Trip = trip,
            PassengerId = passenger.Id,
            Passenger = passenger,
            Status = BookingStatus.Approved,
            RequestedAt = DateTime.UtcNow
        };
        unitOfWork.AdminListingRepository.Trips.Add(trip);
        unitOfWork.AdminListingRepository.Bookings.Add(booking);

        var trips = await new AdminListingService(unitOfWork).GetTripsAsync(
            new AdminTripsRequestDto { DriverId = driver.Id, IsActive = true });
        var bookings = await new AdminListingService(unitOfWork).GetBookingsAsync(
            new AdminBookingsRequestDto
            {
                DriverId = driver.Id,
                UserId = passenger.Id,
                Status = BookingStatus.Approved
            });

        Assert.Single(trips.Items);
        Assert.Equal(1, trips.TotalCount);
        Assert.Single(bookings.Items);
        Assert.Equal(1, bookings.TotalCount);
    }

    private static User User(string email) => new() { Id = Guid.NewGuid(), Email = email, PhoneNumber = "08000000000", Status = UserStatus.Active };
    private static KycVerification Kyc(User user, KycStatus status) => new() { Id = Guid.NewGuid(), UserId = user.Id, User = user, Status = status };
    private static Vehicle Vehicle(User user, bool isActive) => new() { Id = Guid.NewGuid(), UserId = user.Id, User = user, LicensePlateNumber = Guid.NewGuid().ToString("N")[..8], IsActive = isActive };
}
