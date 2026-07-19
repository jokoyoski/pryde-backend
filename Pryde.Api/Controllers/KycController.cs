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
public class KycController(
    IKycService kycService,
    IDojahKycService dojahKycService,
    IAdminListingService adminListingService,
    IAdminPortalService adminPortalService) : ControllerBase
{
    [HttpGet("~/api/v{version:apiVersion}/admin/kyc")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetAdminKyc(
        [FromQuery] AdminKycRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await adminListingService.GetKycAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("~/api/v{version:apiVersion}/admin/kyc/{kycId:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetAdminKycById(
        Guid kycId, CancellationToken cancellationToken)
    {
        return Ok(await adminPortalService.GetKycAsync(kycId, cancellationToken));
    }

    [HttpPost("~/api/v{version:apiVersion}/admin/kyc/{userId:guid}/approve")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> ApproveKyc(Guid userId, CancellationToken cancellationToken)
    {
        var result = await kycService.ApproveAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("~/api/v{version:apiVersion}/admin/kyc/{userId:guid}/reject")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> RejectKyc(
        Guid userId,
        [FromBody] string reason,
        CancellationToken cancellationToken)
    {
        var result = await kycService.RejectAsync(userId, reason, cancellationToken);
        return Ok(result);
    }

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

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        CancellationToken cancellationToken)
    {
        var result = await kycService.GetMineAsync(
            GetUserId(),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("dojah/config")]
    public async Task<IActionResult> GetDojahConfig(
        CancellationToken cancellationToken)
    {
        var result = await dojahKycService.GetConfigAsync(
            GetUserId(),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("dojah/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessDojahWebhook(
        CancellationToken cancellationToken)
    {
        const int maximumPayloadBytes = 1_048_576;
        if (Request.ContentLength > maximumPayloadBytes)
        {
            throw new Pryde.Domain.Common.Exceptions.ValidationException(
                "Webhook payload is too large.");
        }

        var payload = await ReadWebhookPayloadAsync(
            Request.Body,
            maximumPayloadBytes,
            cancellationToken);

        await dojahKycService.ProcessWebhookAsync(
            payload,
            Request.Headers["x-dojah-signature"].FirstOrDefault(),
            Request.Headers["x-dojah-signature-v2"].FirstOrDefault(),
            cancellationToken);

        return Ok();
    }

    private static async Task<byte[]> ReadWebhookPayloadAsync(
        Stream body,
        int maximumPayloadBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        var buffer = new byte[81920];

        while (true)
        {
            var read = await body.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return stream.ToArray();
            }

            if (stream.Length + read > maximumPayloadBytes)
            {
                throw new Pryde.Domain.Common.Exceptions.ValidationException(
                    "Webhook payload is too large.");
            }

            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
