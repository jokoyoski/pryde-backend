using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Api.Controllers.Driver.Authorization;
using Pryde.Services.Service.Interface;
using System.Security.Claims;
using System.Text.Json;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/kyc")]
[Authorize]
public class KycController(
    IKycService kycService,
    IKycProviderService kycProviderService,
    IDojahKycService dojahKycService,
    ISmileIdKycService smileIdKycService,
    ILogger<KycController> logger) : ControllerBase
{
    [HttpGet("mine")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> GetMine(
        CancellationToken cancellationToken)
    {
        var result = await kycService.GetMineAsync(
            GetUserId(),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("session")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> CreateSession(
        CancellationToken cancellationToken)
    {
        return Ok(await kycProviderService.CreateSessionAsync(
            GetUserId(),
            cancellationToken));
    }

    [HttpPost("retry")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> RetryVerification(
        CancellationToken cancellationToken)
    {
        return Ok(await kycProviderService.RetryAsync(
            GetUserId(),
            cancellationToken));
    }

    [HttpGet("dojah/config")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> GetDojahConfig(
        CancellationToken cancellationToken)
    {
        var result = await dojahKycService.GetConfigAsync(
            GetUserId(),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("dojah/retry")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> RetryDojahVerification(
        CancellationToken cancellationToken)
    {
        var result = await dojahKycService.RetryAsync(
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

    [HttpPost("providers/smile-id/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessSmileIdCallback(
        CancellationToken cancellationToken)
    {
        const int maximumPayloadBytes = 1_572_864;

        if (Request.ContentLength > maximumPayloadBytes)
        {
            throw new Pryde.Domain.Common.Exceptions.ValidationException(
                "Webhook payload is too large.");
        }

        var payload = await ReadWebhookPayloadAsync(
            Request.Body,
            maximumPayloadBytes,
            cancellationToken);

        logger.LogInformation(
            "Smile ID callback received. TraceId: {TraceId}; PayloadBytes: {PayloadBytes}.",
            HttpContext.TraceIdentifier,
            payload.Length);

        await smileIdKycService.ProcessCallbackAsync(
            payload,
            cancellationToken);

        logger.LogInformation(
            "Smile ID callback processed successfully. TraceId: {TraceId}.",
            HttpContext.TraceIdentifier);

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
            var read = await body.ReadAsync(
                buffer,
                cancellationToken);

            if (read == 0)
            {
                return stream.ToArray();
            }

            if (stream.Length + read > maximumPayloadBytes)
            {
                throw new Pryde.Domain.Common.Exceptions.ValidationException(
                    "Webhook payload is too large.");
            }

            await stream.WriteAsync(
                buffer.AsMemory(0, read),
                cancellationToken);
        }
    }

    [HttpPost("dojah/webhook/debug")]
    [AllowAnonymous]
    public IActionResult DebugWebhook(
        [FromBody] JsonElement payload)
    {
        return Ok(payload);
    }

    private Guid GetUserId() =>
        Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}