using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
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
        return Ok(roles);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] RoleCreateRequestDto request, CancellationToken cancellationToken)
    {
        var result = await roleService.CreateAsync(request.Name, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RoleCreateRequestDto request, CancellationToken cancellationToken)
    {
        var result = await roleService.UpdateAsync(id, request.Name, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await roleService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}