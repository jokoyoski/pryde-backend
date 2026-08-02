using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/kyc")]
[Authorize]
public class AdminKycController(
    IKycService kycService,
    IAdminListingService adminListingService,
    IAdminPortalService adminPortalService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> GetAdminKyc(
        [FromQuery] AdminKycRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await adminListingService.GetKycAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{kycId:guid}")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> GetAdminKycById(
        Guid kycId,
        CancellationToken cancellationToken)
    {
        return Ok(await adminPortalService.GetKycAsync(kycId, cancellationToken));
    }

    [HttpPost("{userId:guid}/approve")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> ApproveKyc(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await kycService.ApproveAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{userId:guid}/reject")]
    [Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
    public async Task<IActionResult> RejectKyc(
        Guid userId,
        [FromBody] string reason,
        CancellationToken cancellationToken)
    {
        var result = await kycService.RejectAsync(userId, reason, cancellationToken);
        return Ok(result);
    }
}
