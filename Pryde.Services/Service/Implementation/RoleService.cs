using Mapster;
using Pryde.Domain.Common.Exceptions;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Entities;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;
namespace Pryde.Services.Service.Implementation;
public class RoleService(IUnitOfWork unitOfWork) : IRoleService
{
    public async Task<RoleResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await unitOfWork.Roles.GetByIdAsync(id, cancellationToken);
        return role?.Adapt<RoleResponseDto>();
    }

    public async Task<RoleResponseDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var role = await unitOfWork.Roles.GetByNameAsync(name, cancellationToken);
        return role?.Adapt<RoleResponseDto>();
    }

    public async Task<IReadOnlyList<RoleResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var roles = await unitOfWork.Roles.GetAllAsync(cancellationToken);
        return roles.Adapt<List<RoleResponseDto>>();
    }

    public async Task<RoleResponseDto> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Role name cannot be empty.");

        var existing = await unitOfWork.Roles.GetByNameAsync(name, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"A role named '{name}' already exists.");

        var role = new Role { Name = name.Trim() };
        await unitOfWork.Roles.CreateAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return role.Adapt<RoleResponseDto>();
    }

    public async Task<RoleResponseDto> UpdateAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Role name cannot be empty.");

        var role = await unitOfWork.Roles.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), id);

        role.Name = name.Trim();
        unitOfWork.Roles.Update(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return role.Adapt<RoleResponseDto>();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await unitOfWork.Roles.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), id);

        unitOfWork.Roles.Delete(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}