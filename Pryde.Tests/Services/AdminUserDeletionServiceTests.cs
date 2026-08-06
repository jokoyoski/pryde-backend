using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Services.Service.Implementation;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class AdminUserDeletionServiceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ExactlyOneIdentifierIsRequired(
        bool supplyUserId,
        bool supplyEmail)
    {
        var service = new AdminUserDeletionService(new TestUnitOfWork());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.DeleteAsync(
                Guid.NewGuid(),
                supplyUserId ? Guid.NewGuid() : null,
                supplyEmail ? "user@test.local" : null));
    }

    [Fact]
    public async Task MissingUserIsRejected()
    {
        var service = new AdminUserDeletionService(new TestUnitOfWork());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteAsync(
                Guid.NewGuid(),
                null,
                "missing@test.local"));
    }

    [Fact]
    public async Task SuperAdminCannotDeleteSelf()
    {
        var unitOfWork = new TestUnitOfWork();
        var user = AddUser(unitOfWork, "self@test.local");
        var service = new AdminUserDeletionService(unitOfWork);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.DeleteAsync(user.Id, user.Id, null));

        Assert.Contains(user, unitOfWork.UserRepository.Items);
        Assert.Empty(unitOfWork.UserRepository.DeletedWithRelatedDataUserIds);
    }

    [Fact]
    public async Task FinancialOrCompletedRecordsPreventDeletion()
    {
        var unitOfWork = new TestUnitOfWork();
        var user = AddUser(unitOfWork, "protected@test.local");
        unitOfWork.UserRepository.ProtectedDeletionUserIds.Add(user.Id);
        var service = new AdminUserDeletionService(unitOfWork);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.DeleteAsync(Guid.NewGuid(), user.Id, null));

        Assert.Contains(user, unitOfWork.UserRepository.Items);
        Assert.Empty(unitOfWork.UserRepository.DeletedWithRelatedDataUserIds);
    }

    [Fact]
    public async Task UserCanBeDeletedByNormalizedEmailInOneTransaction()
    {
        var unitOfWork = new TestUnitOfWork();
        var user = AddUser(unitOfWork, "target@test.local");
        var service = new AdminUserDeletionService(unitOfWork);

        await service.DeleteAsync(
            Guid.NewGuid(),
            null,
            "  TARGET@test.local  ");

        Assert.DoesNotContain(user, unitOfWork.UserRepository.Items);
        Assert.Equal(
            [user.Id],
            unitOfWork.UserRepository.DeletedWithRelatedDataUserIds);
        Assert.Equal(1, unitOfWork.TransactionCount);
    }

    [Fact]
    public async Task DeletionFailureRollsBackTransaction()
    {
        var unitOfWork = new TestUnitOfWork();
        var user = AddUser(unitOfWork, "rollback@test.local");
        unitOfWork.UserRepository.DeleteWithRelatedDataException =
            new InvalidOperationException("Deletion failed.");
        var service = new AdminUserDeletionService(unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(Guid.NewGuid(), user.Id, null));

        Assert.Contains(
            unitOfWork.UserRepository.Items,
            item => item.Id == user.Id);
        Assert.Equal(1, unitOfWork.TransactionCount);
    }

    private static User AddUser(TestUnitOfWork unitOfWork, string email)
    {
        var user = new User
        {
            Email = email,
            PhoneNumber = Guid.NewGuid().ToString("N")[..20]
        };
        unitOfWork.UserRepository.Items.Add(user);
        return user;
    }
}
