using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/virtual-accounts")]
public class VirtualAccountController(IWalletService walletService) : ControllerBase
{
    [HttpPost("fund")]
    public async Task<IActionResult> Fund(
        [FromBody] FundVirtualAccountRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await walletService.FundVirtualAccountAsync(
            request,
            cancellationToken);

        return Ok(result);
    }
}
