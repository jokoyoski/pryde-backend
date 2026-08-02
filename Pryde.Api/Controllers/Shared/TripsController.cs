using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/trips")]
public class TripsController(ITripService tripService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search(
        [FromQuery] SearchTripsRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await tripService.SearchAsync(request, cancellationToken));
    }

    [HttpGet("{tripId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return Ok(await tripService.GetByIdAsync(tripId, cancellationToken));
    }
}
