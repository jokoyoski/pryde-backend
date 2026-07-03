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
            .FirstOrDefaultAsync(role => role.Id == id, cancellationToken);
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        name = NormalizeName(name);

        return await context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.Name == name, cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Roles
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Roles.AnyAsync(role => role.Id == id, cancellationToken);
    }

    public async Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default)
    {
        role.Name = NormalizeName(role.Name);
        await context.Roles.AddAsync(role, cancellationToken);
        return role;
    }

    public void Update(Role role)
    {
        role.Name = NormalizeName(role.Name);
        context.Roles.Update(role);
    }

    public void Delete(Role role)
    {
        context.Roles.Remove(role);
    }

    private static string NormalizeName(string name) => name.Trim();
}
