using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/webhooks/paystack")]
[AllowAnonymous]
public class PaystackWebhooksController(
    IWalletService walletService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Process(
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await Request.Body.CopyToAsync(stream, cancellationToken);
        await walletService.ProcessPaystackWebhookAsync(
            stream.ToArray(),
            Request.Headers["x-paystack-signature"].FirstOrDefault(),
            cancellationToken);
        return Ok();
    }
}
