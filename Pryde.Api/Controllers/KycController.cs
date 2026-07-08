using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/kyc")]
[Authorize]
public class KycController(IKycService kycService) : ControllerBase
{
    [HttpPost("documents")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocuments(
        [FromForm] KycDocumentUploadRequest request,
        CancellationToken cancellationToken)
    {
        var result = await kycService.UploadDocumentsAsync(
            GetUserId(),
            request,
            cancellationToken);

        return Ok(result);
    }

    [Authorize(Roles = "Admin, SuperAdmin")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        CancellationToken cancellationToken)
    {
        var result = await kycService.GetMineAsync(
            GetUserId(),
            cancellationToken);

        return Ok(result);
    }

    [Authorize(Roles = "Admin, SuperAdmin")]
    [HttpPost("{userId:guid}/reject")]
    public async Task<IActionResult> RejectKyc(Guid userId, [FromBody] string reason, CancellationToken cancellationToken)
    {
        var result = await kycService.RejectAsync(
            userId,
            reason,
            cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost("{userId:guid}/approve")]
    public async Task<IActionResult> AprovedKyc(Guid userId, CancellationToken cancellationToken)
    {
        var result = await kycService.ApproveAsync(
            userId,
            cancellationToken);
        return Ok(result);
    }



    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}