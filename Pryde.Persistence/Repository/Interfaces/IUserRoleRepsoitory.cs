using Pryde.Domain.Entities;
namespace Pryde.Persistence.Repository.Interfaces;
public interface IUserRoleRepository
{
    Task<IReadOnlyList<UserRole>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
    Task<UserRole> CreateAsync(UserRole userRole, CancellationToken cancellationToken = default);
    void Delete(UserRole userRole);
}