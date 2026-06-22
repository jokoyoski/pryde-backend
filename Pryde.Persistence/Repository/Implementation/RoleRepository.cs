using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;
namespace Pryde.Persistence.Repository.Implementations;
public class RoleRepository(PrydeDbContext context) : IRoleRepository
{
    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Roles
            .FirstOrDefaultAsync(
            r => r.Id == id, cancellationToken);
    }
    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        name = name.Trim().ToLower();
        return await context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(
            r => r.Name.ToLower() == name, cancellationToken);
    }
    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Roles
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Roles
            .AnyAsync(r => r.Id == id, cancellationToken);
    }
    public async Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default)
    {
        await context.Roles.AddAsync(role, cancellationToken);
        return role;
    }
    public void Update(Role role)
    {
        context.Roles.Update(role);
    }
    public void Delete(Role role)
    {
        context.Roles.Remove(role);
    }
}