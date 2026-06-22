using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;
namespace Pryde.Persistence.Repository.Implementations;
public class ProfileRepository(PrydeDbContext context) : IProfileRepository
{
    public async Task<Profile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Profiles
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }
    public async Task<Profile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Profiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }
    public async Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Profiles
            .AnyAsync(p => p.UserId == userId, cancellationToken);
    }
    public async Task<Profile> CreateAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        await context.Profiles.AddAsync(profile, cancellationToken);
        return profile;
    }
    public void Update(Profile profile)
    {
        context.Profiles.Update(profile);
    }
    public void Delete(Profile profile)
    {
        context.Profiles.Remove(profile);
    }
}