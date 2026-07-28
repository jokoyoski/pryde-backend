using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/wallets")]
[Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
public class AdminWalletsController : ControllerBase
{
    private readonly IAdminWalletService _adminWalletService;
    private readonly IHostEnvironment _environment;

    public AdminWalletsController(
        IAdminWalletService adminWalletService,
        IHostEnvironment environment)
    {
        _adminWalletService = adminWalletService;
        _environment = environment;
    }

    [HttpPost("fund")]
    public async Task<IActionResult> Fund(
        [FromBody] AdminFundWalletRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsDevelopmentOrTestingEnvironment())
        {
            return NotFound();
        }

        var response = await _adminWalletService.FundWalletAsync(
            request,
            cancellationToken);

        return Ok(response);
    }

    private bool IsDevelopmentOrTestingEnvironment()
    {
        return _environment.IsDevelopment() ||
            _environment.IsEnvironment("Testing") ||
            _environment.IsEnvironment("QA");
    }
}
