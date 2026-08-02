using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Api.Controllers.Driver.Authorization;
using Pryde.Contracts.RequestModels;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/banks")]
[Authorize(Policy = AuthorizationPolicies.EmailVerified)]
public class BanksController : ControllerBase
{
    private readonly IDriverBankAccountService _driverBankAccountService;

    public BanksController(
        IDriverBankAccountService driverBankAccountService)
    {
        _driverBankAccountService = driverBankAccountService;
    }

    [HttpGet]
    public async Task<IActionResult> GetBanks(
        CancellationToken cancellationToken)
    {
        var response = await _driverBankAccountService.GetBanksAsync(
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("resolve")]
    public async Task<IActionResult> ResolveAccount(
        [FromBody] ResolveBankAccountRequestDto request,
        CancellationToken cancellationToken)
    {
        var response = await _driverBankAccountService.ResolveAccountAsync(
            request,
            cancellationToken);

        return Ok(response);
    }
}
