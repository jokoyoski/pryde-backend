using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Api.Controllers.Driver.Authorization;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/driver/bank-accounts")]
[Authorize(
    Roles = RoleNames.Driver,
    Policy = AuthorizationPolicies.EmailVerified)]
public class DriverBankAccountsController : ControllerBase
{
    private readonly IDriverBankAccountService _driverBankAccountService;

    public DriverBankAccountsController(
        IDriverBankAccountService driverBankAccountService)
    {
        _driverBankAccountService = driverBankAccountService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateDriverBankAccountRequestDto request,
        CancellationToken cancellationToken)
    {
        var response = await _driverBankAccountService.CreateAsync(
            GetUserId(),
            request,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }

    [HttpGet]
    public async Task<IActionResult> GetMine(
        CancellationToken cancellationToken)
    {
        var response = await _driverBankAccountService.GetMineAsync(
            GetUserId(),
            cancellationToken);

        return Ok(response);
    }

    private Guid GetUserId()
    {
        return Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
