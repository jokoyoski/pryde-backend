using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/recurring-trips")]
[Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
public class AdminRecurringTripsController(
    IRecurringTripService recurringTripService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminRecurringTripsRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await recurringTripService.AdminGetAllAsync(
            request, cancellationToken));

    [HttpGet("{recurringTripId:guid}")]
    public async Task<IActionResult> Get(
        Guid recurringTripId,
        CancellationToken cancellationToken) =>
        Ok(await recurringTripService.AdminGetByIdAsync(
            recurringTripId, cancellationToken));
}
