using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/bookings")]
[Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
public class AdminBookingsController(IAdminListingService adminListingService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminBookingsRequestDto request, CancellationToken cancellationToken) =>
        Ok(await adminListingService.GetBookingsAsync(request, cancellationToken));

    [HttpGet("{bookingId:guid}")]
    public async Task<IActionResult> Get(Guid bookingId, CancellationToken cancellationToken) =>
        Ok(await adminListingService.GetBookingAsync(bookingId, cancellationToken));
}
