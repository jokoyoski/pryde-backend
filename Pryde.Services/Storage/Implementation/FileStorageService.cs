using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using Pryde.Services.Settings;
using Pryde.Services.Storage.Enums;
using Pryde.Services.Storage.Interface;
using Pryde.Services.Storage.Models;

namespace Pryde.Services.Storage.Implementation;

public sealed class CloudinaryFileStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryFileStorageService(IOptions<CloudinarySettings> options)
    {
        var settings = options.Value;

        var account = new Account(
            settings.CloudName,
            settings.ApiKey,
            settings.ApiSecret);

        _cloudinary = new Cloudinary(account);
    }

    public async Task<FileUploadResult> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        FileCategory category,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        var publicId =
            $"{GetFolder(category, ownerId)}/{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid():N}";

        string? fileKey;
        string? publicUrl;
        string? errorMessage;

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                PublicId = publicId,
                Overwrite = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
            fileKey = result.PublicId;
            publicUrl = result.SecureUrl?.ToString();
            errorMessage = result.Error?.Message;
        }
        else if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            var uploadParams = new VideoUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                PublicId = publicId,
                Overwrite = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
            fileKey = result.PublicId;
            publicUrl = result.SecureUrl?.ToString();
            errorMessage = result.Error?.Message;
        }
        else
        {
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                PublicId = publicId,
                Overwrite = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams, "raw", cancellationToken);
            fileKey = result.PublicId;
            publicUrl = result.SecureUrl?.ToString();
            errorMessage = result.Error?.Message;
        }

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }

        if (string.IsNullOrWhiteSpace(fileKey))
        {
            throw new InvalidOperationException("Cloudinary upload failed.");
        }

        return new FileUploadResult(
            fileKey,
            publicUrl
                ?? throw new InvalidOperationException("Cloudinary did not return a secure URL."));
    }

    public Task<string> GetReadUrlAsync(
        string fileKey,
        FileCategory category,
        CancellationToken cancellationToken = default)
    {
        var url = category switch
        {
            FileCategory.VehicleVideo =>
                _cloudinary.Api.UrlVideoUp.BuildUrl(fileKey),

            FileCategory.VehicleDocument or
            FileCategory.KycDriverLicense or
            FileCategory.KycSecondaryId =>
                _cloudinary.Api.UrlImgUp.ResourceType("raw").BuildUrl(fileKey),

            _ =>
                _cloudinary.Api.UrlImgUp.BuildUrl(fileKey)
        };

        return Task.FromResult(url);
    }

    public async Task DeleteAsync(
        string fileKey,
        FileCategory category,
        CancellationToken cancellationToken = default)
    {
        var deletionParams = new DeletionParams(fileKey)
        {
            ResourceType = category switch
            {
                FileCategory.VehicleVideo => ResourceType.Video,
                FileCategory.VehicleDocument or
                FileCategory.KycDriverLicense or
                FileCategory.KycSecondaryId => ResourceType.Raw,
                _ => ResourceType.Image
            }
        };

        var result = await _cloudinary.DestroyAsync(deletionParams);

        if (!string.Equals(result.Result, "ok", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(result.Result, "not found", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Delete failed: {result.Result}");
        }
    }

    private static string GetFolder(FileCategory category, Guid ownerId)
    {
        return category switch
        {
            FileCategory.ProfilePhoto =>
                $"profiles/{ownerId}",

            FileCategory.VehiclePhoto =>
                $"vehicles/{ownerId}/photos",

            FileCategory.VehicleVideo =>
                $"vehicles/{ownerId}/videos",

            FileCategory.VehicleDocument =>
                $"vehicles/{ownerId}/documents",

            FileCategory.KycBiometric =>
                $"kyc/{ownerId}/biometric",

            FileCategory.KycDriverLicense =>
                $"kyc/{ownerId}/driver-license",

            FileCategory.KycSecondaryId =>
                $"kyc/{ownerId}/secondary-id",

            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
    }
}
