using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;
using Microsoft.Extensions.Options;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/wallets")]
[Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
public class AdminWalletsController : ControllerBase
{
    private readonly IAdminWalletService _adminWalletService;
    private readonly WalletTestingSettings _walletTestingSettings;

    public AdminWalletsController(
        IAdminWalletService adminWalletService,
        IOptions<WalletTestingSettings> walletTestingSettings)
    {
        _adminWalletService = adminWalletService;
        _walletTestingSettings = walletTestingSettings.Value;
    }

    [HttpPost("fund")]
    public async Task<IActionResult> Fund(
        [FromBody] AdminFundWalletRequest request,
        CancellationToken cancellationToken)
    {
        if (!_walletTestingSettings.EnableManualFunding)
        {
            return NotFound();
        }

        var response = await _adminWalletService.FundWalletAsync(
            request,
            cancellationToken);

        return Ok(response);
    }
}
