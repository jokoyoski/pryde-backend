using Pryde.Domain.Entities;
namespace Pryde.Persistence.Repository.Interfaces;
public interface IProfileRepository
{
    Task<Profile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Profile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Profile> CreateAsync(Profile profile, CancellationToken cancellationToken = default);
    void Update(Profile profile);
    void Delete(Profile profile);
}