using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class UnitOfWork(
    PrydeDbContext context,
    IUserRepository users,
    IRoleRepository roles,
    IUserRoleRepository userRoles,
    IProfileRepository profiles,
    IKycVerificationRepository kycVerifications,
    IVehicleRepository vehicles,
    IVehicleDocumentRepository vehicleDocuments,
    IRefreshTokenRepository refreshTokenRepository)
    : IUnitOfWork
{
    public IUserRepository Users { get; } = users;
    public IRoleRepository Roles { get; } = roles;
    public IUserRoleRepository UserRoles { get; } = userRoles;
    public IProfileRepository Profiles { get; } = profiles;
    public IKycVerificationRepository KycVerifications { get; } = kycVerifications;
    public IVehicleRepository Vehicles { get; } = vehicles;
    public IVehicleDocumentRepository VehicleDocuments { get; } = vehicleDocuments;
    public IRefreshTokenRepository RefreshTokens { get; } = refreshTokenRepository;


    public async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }
}