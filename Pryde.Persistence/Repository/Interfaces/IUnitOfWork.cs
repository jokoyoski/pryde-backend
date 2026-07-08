using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IRoleRepository Roles { get; }
    IUserRoleRepository UserRoles { get; }
    IProfileRepository Profiles { get; }
    IKycVerificationRepository KycVerifications { get; }
    IVehicleRepository Vehicles { get; }
    IVehicleDocumentRepository VehicleDocuments { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    IPasswordResetCodeRepository PasswordResetCodes { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}