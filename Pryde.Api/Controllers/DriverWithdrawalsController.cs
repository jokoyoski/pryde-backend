using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Api.Authorization;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/driver/withdrawals")]
[Authorize(
    Roles = RoleNames.Driver,
    Policy = AuthorizationPolicies.EmailVerified)]
public class DriverWithdrawalsController : ControllerBase
{
    private readonly IDriverWithdrawalService _driverWithdrawalService;

    public DriverWithdrawalsController(
        IDriverWithdrawalService driverWithdrawalService)
    {
        _driverWithdrawalService = driverWithdrawalService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateDriverWithdrawalRequestDto request,
        CancellationToken cancellationToken)
    {
        var response = await _driverWithdrawalService.CreateAsync(
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
        var response = await _driverWithdrawalService.GetMineAsync(
            GetUserId(),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _driverWithdrawalService.GetByIdAsync(
            GetUserId(),
            id,
            cancellationToken);

        return Ok(response);
    }

    private Guid GetUserId()
    {
        return Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
