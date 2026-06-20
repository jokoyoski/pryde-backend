// UserRoleRepository.cs
using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;
namespace Pryde.Persistence.Repository.Implementations;
public class UserRoleRepository(PrydeDbContext context) : IUserRoleRepository
{
    public async Task<IReadOnlyList<UserRole>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.UserRoles
            .AsNoTracking()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken);
    }
    public async Task<bool> ExistsAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        return await context.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);
    }
    public async Task<UserRole> CreateAsync(UserRole userRole, CancellationToken cancellationToken = default)
    {
        await context.UserRoles.AddAsync(userRole, cancellationToken);
        return userRole;
    }
    public void Delete(UserRole userRole)
    {
        context.UserRoles.Remove(userRole);
    }
}