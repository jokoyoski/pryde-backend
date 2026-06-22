using Pryde.Domain.Entities;

namespace Pryde.Persistence.Repository.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default);

    void Update(Role role);

    void Delete(Role role);
}