using Pryde.Domain.Entities;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class RoleService(IUnitOfWork unitOfWork) : IRoleService
{
    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await unitOfWork.Roles.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await unitOfWork.Roles.GetByNameAsync(name, cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await unitOfWork.Roles.GetAllAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await unitOfWork.Roles.ExistsAsync(id, cancellationToken);
    }

    public async Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default)
    {
        await unitOfWork.Roles.CreateAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return role;
    }

    public async Task UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        unitOfWork.Roles.Update(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await unitOfWork.Roles.GetByIdAsync(id, cancellationToken);

        if (role is null)
            return;

        unitOfWork.Roles.Delete(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}