using Microsoft.AspNetCore.Http;

namespace Pryde.Contracts.RequestModels;

public sealed class ProfilePhotoUploadRequest
{
    public IFormFile Photo { get; init; } = default!;
}