using Pryde.Domain.Enums;

namespace Pryde.Contracts.RequestModels;

public class SelectRolesRequestDto
{
    public List<RoleType> Roles { get; set; } = [];
}
