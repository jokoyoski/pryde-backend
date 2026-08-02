using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/trips")]
[Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
public class AdminTripsController(IAdminListingService adminListingService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminTripsRequestDto request, CancellationToken cancellationToken) =>
        Ok(await adminListingService.GetTripsAsync(request, cancellationToken));

    [HttpGet("{tripId:guid}")]
    public async Task<IActionResult> Get(Guid tripId, CancellationToken cancellationToken) =>
        Ok(await adminListingService.GetTripAsync(tripId, cancellationToken));
}
