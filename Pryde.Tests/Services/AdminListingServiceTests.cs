using Pryde.Contracts.RequestModels;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class AdminListingServiceTests
{
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

    private static User User(string email) => new() { Id = Guid.NewGuid(), Email = email, PhoneNumber = "08000000000", Status = UserStatus.Active };
    private static KycVerification Kyc(User user, KycStatus status) => new() { Id = Guid.NewGuid(), UserId = user.Id, User = user, Status = status };
    private static Vehicle Vehicle(User user, bool isActive) => new() { Id = Guid.NewGuid(), UserId = user.Id, User = user, LicensePlateNumber = Guid.NewGuid().ToString("N")[..8], IsActive = isActive };
}
