using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Pryde.Domain.DTOs.ResponseModels;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/roles")]
public class RoleController(IRoleService roleService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var roles = await roleService.GetAllAsync(cancellationToken);

        var response = roles
            .Select(r => new RoleResponseDto { Id = r.Id, Name = r.Name })
            .ToList();

        return Ok(response);
    }
}