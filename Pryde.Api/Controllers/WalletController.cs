using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/wallet")]
[Authorize]
public class WalletController(IWalletService walletService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await walletService.GetMineAsync(GetUserId(), cancellationToken));
    }

    [HttpGet("mine/transactions")]
    public async Task<IActionResult> GetTransactions(CancellationToken cancellationToken)
    {
        return Ok(await walletService.GetTransactionsAsync(GetUserId(), cancellationToken));
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
