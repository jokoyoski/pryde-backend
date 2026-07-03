using Pryde.Domain.Entities;

namespace Pryde.Services.Service.Interface;

public interface IRoleService
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default);

    Task UpdateAsync(Role role, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}