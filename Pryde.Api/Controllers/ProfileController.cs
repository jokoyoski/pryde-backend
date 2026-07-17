using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Services.Service.Interface;
using Pryde.Services.Storage.Enums;
using Pryde.Services.Storage.Interface;
namespace Pryde.Api.Controllers.V1;
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/profile")]
[Authorize]
public class ProfileController(
    IProfileService profileService,
    IFileStorageService fileStorageService,
    IUserService userService,
    IAdminListingService adminListingService) : ControllerBase
{
    [HttpGet("~/api/v{version:apiVersion}/admin/users")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken)
    {
        var users = await userService.GetAllAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("~/api/v{version:apiVersion}/admin/users/paged")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetPagedUsers(
        [FromQuery] AdminUsersRequestDto request,
        CancellationToken cancellationToken)
    {
        var users = await adminListingService.GetUsersAsync(request, cancellationToken);
        return Ok(users);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await profileService.GetMineAsync(GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpPut("mine")]
    public async Task<IActionResult> Update([FromBody] ProfileUpdateRequestDto request, CancellationToken cancellationToken)
    {
        var result = await profileService.UpdateAsync(GetUserId(), request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("mine/photo")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdatePhoto([FromForm] ProfilePhotoUploadRequest request,
    CancellationToken cancellationToken)
    {
        if (request.Photo is null || request.Photo.Length == 0)
        {
            throw new ValidationException("A photo file is required.");
        }

        var userId = GetUserId();

        await using var stream = request.Photo.OpenReadStream();

        var upload = await fileStorageService.UploadAsync(
            stream,
            request.Photo.FileName,
            request.Photo.ContentType,
            FileCategory.ProfilePhoto,
            userId,
            cancellationToken);

        var result = await profileService.UpdatePhotoAsync(
            userId,
            upload.PublicUrl,
            cancellationToken);

        return Ok(result);
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
