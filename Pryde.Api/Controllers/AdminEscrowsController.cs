using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/escrows")]
[Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
public class AdminEscrowsController(IFinancialService financialService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminEscrowsRequestDto request, CancellationToken cancellationToken) =>
        Ok(await financialService.GetEscrowsAsync(request, cancellationToken));

    [HttpGet("{escrowId:guid}")]
    public async Task<IActionResult> Get(Guid escrowId, CancellationToken cancellationToken) =>
        Ok(await financialService.GetEscrowAsync(escrowId, cancellationToken));
}
