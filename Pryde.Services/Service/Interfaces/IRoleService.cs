using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;
public interface IRoleService
{
    Task<RoleResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RoleResponseDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RoleResponseDto> CreateAsync(string name, CancellationToken cancellationToken = default);
    Task<RoleResponseDto> UpdateAsync(Guid id, string name, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}