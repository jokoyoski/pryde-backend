using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Services.Service.Interface;
using Pryde.Api.Authorization;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/virtual-accounts")]
public class VirtualAccountController(
    IWalletService walletService,
    IHostEnvironment environment) : ControllerBase
{
    [HttpGet("mine")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await walletService.GetVirtualAccountAsync(GetUserId(), cancellationToken));
    }

    [HttpPost("fund")]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    public async Task<IActionResult> Fund(
        [FromBody] FundVirtualAccountRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        var result = await walletService.FundVirtualAccountAsync(
            GetUserId(),
            request,
            cancellationToken);

        return Ok(result);
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
